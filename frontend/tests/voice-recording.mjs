import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch({ args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] })
const context = await browser.newContext({ baseURL, viewport: { width: 320, height: 810 }, permissions: ['microphone'] })
const page = await context.newPage()
const username = `voice-${Date.now().toString(36)}`
const password = 'Budz-voice-2026'
await context.addInitScript(() => {
  const getMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices)
  const revoke = URL.revokeObjectURL.bind(URL)
  window.budzTracks = []
  window.budzRevoked = []
  navigator.mediaDevices.getUserMedia = async options => {
    if (window.budzMicError) throw new DOMException('Simulated browser error', window.budzMicError)
    const stream = await getMedia(options)
    window.budzTracks.push(...stream.getTracks())
    return stream
  }
  URL.revokeObjectURL = url => {
    window.budzRevoked.push(url)
    revoke(url)
  }
  const Recorder = window.MediaRecorder
  window.MediaRecorder = class extends Recorder {
    constructor(...args) {
      super(...args)
      window.budzRecorder = this
      window.budzChunks = 0
      this.addEventListener('dataavailable', event => { if (event.data.size > 0) window.budzChunks++ })
    }
  }
})

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)
  let uploads = 0
  page.on('request', request => {
    if (/^multipart/i.test(request.headers()['content-type'] ?? '')) uploads++
  })
  await page.goto('/chats')
  const workspace = page.locator('.workspace')
  await workspace.filter({ has: page.getByRole('heading', { name: 'С чего начнём?' }) }).waitFor()
  const composer = workspace.getByRole('textbox', { name: 'Сообщение для Budz' })
  const mic = workspace.getByRole('button', { name: 'Записать голос', exact: true })
  const panel = workspace.getByRole('group', { name: 'Запись голоса' })
  const voiceState = panel.getByText('Идёт запись', { exact: true })
  const stopped = () => page.waitForFunction(() => window.budzTracks.every(track => track.readyState === 'ended'))
  const start = async () => {
    await mic.click()
    await voiceState.waitFor({ timeout: 7000 }).catch(async failure => {
      const state = await panel.allTextContents()
      throw new Error(`Recording did not start: ${JSON.stringify(state)}`, { cause: failure })
    })
  }
  assert.equal(await page.evaluate(() => window.budzTracks.length), 0)
  await composer.fill('Черновик сохранится')
  await workspace.getByLabel('Выбор файла').setInputFiles({ name: 'note.txt', mimeType: 'text/plain', buffer: Buffer.from('Текст') })
  await start()
  const timer = panel.locator('.voice-time')
  await page.waitForFunction(() => document.querySelector('.voice-time')?.textContent !== '0:00')
  assert.match(await timer.textContent(), /^\d+:\d\d$/)
  await panel.getByRole('button', { name: 'Завершить запись' }).click()
  await panel.getByText('Голосовая запись', { exact: true }).waitFor()
  await stopped()
  const audio = panel.locator('audio')
  const url = await audio.getAttribute('src')
  assert.match(url, /^blob:/)
  await panel.getByRole('button', { name: 'Прослушать' }).click()
  await page.waitForFunction(() => document.querySelector('.voice-panel audio')?.currentTime > 0)
  await panel.getByRole('button', { name: 'Записать заново' }).click()
  const replace = page.getByRole('dialog', { name: 'Заменить запись?' })
  await replace.getByRole('button', { name: 'Оставить запись' }).click()
  assert.equal(await audio.getAttribute('src'), url)
  await panel.getByRole('button', { name: 'Записать заново' }).click()
  await replace.getByRole('button', { name: 'Записать заново' }).click()
  await voiceState.waitFor()
  await panel.getByRole('button', { name: 'Отменить запись' }).click()
  await panel.getByText('Голосовая запись', { exact: true }).waitFor()
  assert.equal(await audio.getAttribute('src'), url)
  await stopped()
  await panel.getByRole('button', { name: 'Удалить запись' }).click()
  await panel.waitFor({ state: 'hidden' })
  assert.ok(await page.evaluate(old => window.budzRevoked.includes(old), url))

  for (const [error, message] of [
    ['NotAllowedError', /Доступ к микрофону запрещён/],
    ['NotFoundError', /Микрофон не найден/],
    ['NotReadableError', /Микрофон занят или недоступен/],
  ]) {
    await page.evaluate(error => { window.budzMicError = error }, error)
    await mic.click()
    await panel.getByRole('alert').filter({ hasText: message }).waitFor()
  }
  await page.evaluate(() => { delete window.budzMicError })
  await start()
  await page.evaluate(() => {
    Object.defineProperty(document, 'hidden', { configurable: true, value: true })
    document.dispatchEvent(new Event('visibilitychange'))
    delete document.hidden
  })
  await panel.getByRole('alert').filter({ hasText: /Запись прервана/ }).waitFor()
  await stopped()
  await start()
  await page.evaluate(() => window.budzTracks.at(-1).dispatchEvent(new Event('ended')))
  await panel.getByRole('alert').filter({ hasText: /Микрофон отключился/ }).waitFor()
  await stopped()

  await start()
  await page.evaluate(() => window.budzRecorder.dispatchEvent(new BlobEvent('dataavailable', {
    data: new Blob([new Uint8Array(12 * 1024 * 1024 + 1)]),
  })))
  await panel.getByRole('alert').filter({ hasText: /Запись превышает допустимый размер/ }).waitFor()
  await stopped()

  await page.clock.install()
  await start()
  await page.waitForFunction(() => window.budzRecorder.state === 'recording' && window.budzChunks > 0)
  await page.clock.runFor(120_000)
  await panel.getByText('Голосовая запись', { exact: true }).waitFor()
  assert.equal(await timer.textContent(), '2:00')
  await stopped()
  await panel.getByRole('button', { name: 'Удалить запись' }).click()

  await start()
  const switchLayout = async () => {
    await page.getByRole('button', { name: 'Меню аккаунта', exact: true }).click()
    await page.getByRole('menuitemradio', { name: 'Текущий Budz' }).click()
  }
  await switchLayout()
  const guard = page.getByRole('dialog', { name: 'Сменить интерфейс?' })
  await guard.getByRole('button', { name: 'Остаться' }).click()
  await voiceState.waitFor()
  await switchLayout()
  await guard.getByRole('button', { name: 'Прервать и переключить' }).click()
  const current = page.locator('.workspace[data-interface="current"]')
  await current.waitFor()
  await stopped()
  assert.equal(await composer.inputValue(), 'Черновик сохранится')
  await current.getByText('note.txt', { exact: true }).waitFor()
  await start()
  await page.getByRole('button', { name: 'Меню аккаунта', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Профиль', exact: true }).click()
  await page.waitForURL('**/profile')
  await stopped()
  assert.equal(uploads, 0)
  console.log('Verified browser recording, local playback, permission errors, switching guard and microphone cleanup.')
} finally { await browser.close() }
