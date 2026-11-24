import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const browser = await chromium.launch({ args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] })
const context = await browser.newContext({
  baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080', viewport: { width: 1280, height: 850 }, permissions: ['microphone'],
})
const page = await context.newPage()
const username = `speech-${Date.now().toString(36)}`
const password = 'Budz-speech-2026'
const firstText = ['Первый пример чтения.', '- Первый пункт\n- Второй пункт',
  'https://test.example.invalid/path?technical=parameter',
  ['```javascript', 'const hiddenTechnicalCode = 123;', '```'].join('\n')].join('\n\n')
await context.addInitScript(() => {
  const state = { spoken: [], cancelled: 0, paused: false, current: null, onlyRemote: false }
  window.budzSpeech = state
  Object.defineProperty(window, 'speechSynthesis', { value: {
    get paused() { return state.paused },
    getVoices() {
      const remote = { name: 'remote', lang: 'ru-RU', localService: false }
      const local = { name: 'local', lang: 'ru-RU', localService: true }
      return state.onlyRemote ? [remote] : [remote, local]
    },
    speak(item) { state.current = item; state.spoken.push({ text: item.text, voice: item.voice.name, language: item.lang }) },
    cancel() { state.cancelled++; state.current = null },
    pause() { state.paused = true },
    resume() { state.paused = false },
  } })
  Object.defineProperty(window, 'SpeechSynthesisUtterance', { value: class {
    constructor(text) { this.text = text }
  } })
})

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  const imported = await context.request.post('/api/v1/data/import', { headers, multipart: {
    file: { name: 'speech.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify([{
      name: 'Ответы для озвучивания', uuid: username, chat_messages: [
        { sender: 'human', text: 'Покажи два примера.' },
        { sender: 'assistant', text: firstText },
        { sender: 'human', text: 'И ещё.' },
        { sender: 'assistant', text: 'Второй пример чтения.' },
      ],
    }])) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const chat = (await imported.json()).chatIds[0]
  const next = await context.request.post('/api/v1/chats', { headers, data: {} })
  const otherChat = (await next.json()).id
  assert.equal((await context.request.patch(`/api/v1/chats/${otherChat}`, { headers, data: { title: 'Перейти сюда' } })).status(), 204)
  await page.goto(`/chats/${chat}`)
  const workspace = page.locator('.workspace')
  const first = workspace.locator('.message.assistant').filter({ hasText: 'Первый пример чтения.' })
  const second = workspace.locator('.message.assistant').filter({ hasText: 'Второй пример чтения.' })
  const speak = target => target.getByRole('button', { name: 'Озвучить', exact: true }).click()
  await speak(first)
  await first.getByRole('button', { name: 'Пауза', exact: true }).click()
  assert.ok(await page.evaluate(() => window.budzSpeech.paused))
  await first.getByRole('button', { name: 'Продолжить', exact: true }).click()
  assert.equal(await page.evaluate(() => window.budzSpeech.paused), false)
  const segments = await page.evaluate(() => {
    const state = window.budzSpeech
    for (let index = 0; state.current && index < 25; index++) state.current.onend()
    return state.spoken
  })
  assert.ok(segments.every(segment => segment.voice === 'local' && segment.language === 'ru-RU'))
  assert.match(segments.map(segment => segment.text).join(' '), /Первый пункт Второй пункт.*ссылка.*Блок кода\./)
  assert.doesNotMatch(segments.map(segment => segment.text).join(' '), /technical=|hiddenTechnicalCode/)
  await speak(first)
  const switches = await page.evaluate(() => window.budzSpeech.cancelled)
  await speak(second)
  assert.ok(await page.evaluate(() => window.budzSpeech.current.text.includes('Второй пример')))
  assert.equal(await page.evaluate(() => window.budzSpeech.cancelled), switches + 1)
  await second.getByRole('button', { name: 'Остановить озвучивание' }).click()
  assert.equal(await page.evaluate(() => window.budzSpeech.current), null)

  await speak(first)
  await workspace.getByRole('button', { name: 'Записать голос', exact: true }).click()
  await workspace.getByText('Идёт запись', { exact: true }).waitFor()
  assert.equal(await page.evaluate(() => window.budzSpeech.current), null)
  assert.ok(await first.getByRole('button', { name: 'Озвучить', exact: true }).isDisabled())
  await workspace.getByRole('button', { name: 'Отменить запись' }).click()

  await speak(first)
  await page.getByRole('link', { name: 'Перейти сюда', exact: true }).click()
  await page.waitForURL(`**/chats/${otherChat}`)
  await page.waitForFunction(() => window.budzSpeech.current === null)
  await page.getByRole('link', { name: 'Ответы для озвучивания', exact: true }).click()
  await page.waitForURL(`**/chats/${chat}`)

  await page.evaluate(() => { window.budzSpeech.onlyRemote = true })
  await speak(first)
  await first.getByText('Озвучивание недоступно на этом устройстве.', { exact: true }).waitFor()
  assert.equal(await page.evaluate(() => window.budzSpeech.current), null)
  await first.getByText('Первый пример чтения.', { exact: true }).waitFor()
  await page.evaluate(() => { window.budzSpeech.onlyRemote = false })
  await speak(first)
  await page.getByRole('button', { name: 'Меню аккаунта' }).click()
  await page.getByRole('menuitem', { name: 'Выйти', exact: true }).click()
  await page.waitForURL('**/login')
  assert.equal(await page.evaluate(() => window.budzSpeech.current), null)
  console.log('Verified local-only speech, readable Markdown, exclusive playback, pause, mic interruption, navigation and sign-out.')
} finally { await browser.close() }
