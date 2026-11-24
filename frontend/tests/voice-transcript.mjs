import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch({ args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] })
const context = await browser.newContext({ baseURL, viewport: { width: 1280, height: 850 }, permissions: ['microphone'] })
const page = await context.newPage()
const username = `dictation-${Date.now().toString(36)}`
const password = 'Budz-dictation-2026'

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)
  async function chat(title, temporary = false) {
    const response = await context.request.post('/api/v1/chats', { headers, data: { temporary } })
    assert.equal(response.status(), 201)
    const id = (await response.json()).id
    if (!temporary) {
      assert.equal((await context.request.patch(`/api/v1/chats/${id}`, { headers, data: { title } })).status(), 204)
    }
    return id
  }
  const first = await chat('Основной разговор')
  const second = await chat('Второй разговор')
  const temporary = await chat('', true)
  const modes = ['timeout', 'silent', 'ok', 'overflow', 'delay', 'delay', 'ok']
  const posted = []
  const submitted = []
  let audioClip = ''
  let resolveDelayed
  let delivered = 0
  await page.route('**/api/v1/voice/transcriptions', async route => {
    posted.push(route.request().postDataBuffer())
    const mode = modes.shift()
    if (mode === 'timeout') return route.fulfill({ status: 503, json: { code: 'voice_timeout', title: 'Timeout' } })
    if (mode === 'silent') return route.fulfill({ status: 200, json: { text: ' ' } })
    if (mode === 'overflow') return route.fulfill({ status: 200, json: { text: 'Длинная фраза', language: 'ru' } })
    if (mode === 'delay') {
      await new Promise(resolve => { resolveDelayed = resolve })
      try { await route.fulfill({ status: 200, json: { text: 'Опоздавший результат', language: 'ru' } }) } catch { /* A cancelled fetch is expected. */ }
      delivered++
      return
    }
    return route.continue()
  })
  page.on('request', request => {
    if (request.method() === 'POST' && /\/api\/v1\/chats\/[^/]+\/messages$/.test(new URL(request.url()).pathname)) {
      submitted.push(request.postData())
    }
  })

  await page.goto(`/chats/${first}`)
  const focus = page.locator('.workspace[data-interface="focus"]')
  const composer = focus.getByRole('textbox', { name: 'Сообщение для Budz' })
  const panel = focus.getByRole('group', { name: 'Запись голоса' })
  const mic = focus.getByRole('button', { name: 'Записать голос' })
  const preview = panel.getByText('Голосовая запись', { exact: true })
  const recognize = panel.getByRole('button', { name: 'Расшифровать', exact: true })
  const retry = panel.getByRole('button', { name: 'Повторить распознавание' })
  const lab = panel.getByRole('textbox', { name: 'Расшифровка' })
  const insert = panel.getByRole('button', { name: 'Вставить в черновик' })
  async function record() {
    await mic.click()
    await panel.getByText('Идёт запись', { exact: true }).waitFor()
    await page.waitForFunction(() => document.querySelector('.voice-time')?.textContent !== '0:00')
    await panel.getByRole('button', { name: 'Завершить запись' }).click()
    await preview.waitFor()
  }
  await composer.fill('До После')
  await composer.evaluate(el => el.setSelectionRange(2, 2))
  await record()
  assert.equal(posted.length, 0)
  assert.equal(submitted.length, 0)
  audioClip = await panel.locator('audio').getAttribute('src')
  await panel.getByText(/автоматически появится в черновике/).waitFor()
  await panel.getByLabel('Язык записи').selectOption('ru')
  await recognize.click()
  await panel.getByRole('alert').filter({ hasText: /заняло слишком много времени/ }).waitFor()
  assert.equal(await panel.locator('audio').getAttribute('src'), audioClip)
  assert.equal(await composer.inputValue(), 'До После')
  await retry.click()
  await panel.getByRole('alert').filter({ hasText: /не разобрана речь/ }).waitFor()
  assert.equal(await panel.locator('audio').getAttribute('src'), audioClip)
  await retry.click()
  await panel.waitFor({ state: 'hidden' })
  assert.equal(await composer.inputValue(), 'До Это тестовая расшифровка Budz. После')
  assert.match(posted[2].toString('latin1'), /name="language"\r\n\r\nru/)
  assert.ok(await composer.evaluate(el => el === document.activeElement))
  assert.equal(submitted.length, 0)
  await composer.fill('До Исправленное слово После')
  await composer.press('Control+Enter')
  assert.equal(submitted.length, 0)
  await composer.fill('До Исправленное слово После')
  await composer.press('Enter')
  await focus.locator('.message.user').getByText('До Исправленное слово После', { exact: true }).waitFor()
  assert.equal(submitted.length, 1)
  assert.doesNotMatch(submitted[0], /filename=/)

  await composer.fill('a'.repeat(10_000))
  await record()
  await recognize.click()
  await panel.getByRole('alert').filter({ hasText: /Сократите расшифровку/ }).waitFor()
  assert.equal(await lab.inputValue(), 'Длинная фраза')
  assert.equal((await composer.inputValue()).length, 10000)
  await composer.fill('Место для')
  await lab.fill('исправления')
  await insert.click()
  await panel.waitFor({ state: 'hidden' })
  assert.equal(await composer.inputValue(), 'Место для исправления')
  assert.equal(submitted.length, 1)

  await composer.fill('Текст при отмене')
  await record()
  await recognize.click()
  await panel.getByText('Распознаём речь…', { exact: true }).waitFor()
  await panel.getByText(/Обработка занимает больше времени/).waitFor({ timeout: 9000 })
  assert.equal(await recognize.count(), 0)
  await panel.getByRole('button', { name: 'Отменить распознавание' }).click()
  resolveDelayed()
  await preview.waitFor()
  assert.equal(await lab.count(), 0)
  assert.equal(await composer.inputValue(), 'Текст при отмене')
  assert.equal(submitted.length, 1)

  await recognize.click()
  await panel.getByText('Распознаём речь…', { exact: true }).waitFor()
  const destination = focus.getByRole('link', { name: 'Второй разговор', exact: true })
  await destination.click()
  const dialog = page.getByRole('dialog', { name: 'Перейти в другой диалог?' })
  await dialog.getByRole('button', { name: 'Остаться' }).click()
  assert.ok(page.url().endsWith(first))
  await destination.click()
  await dialog.getByRole('button', { name: 'Прервать и переключить' }).click()
  await page.waitForURL(`**/chats/${second}`)
  resolveDelayed()
  assert.equal(await lab.count(), 0)
  assert.doesNotMatch(await composer.inputValue(), /Опоздавший результат/)
  assert.equal(submitted.length, 1)

  await page.setViewportSize({ width: 320, height: 780 })
  await page.goto(`/chats/${temporary}`)
  await focus.getByText('Временный диалог', { exact: true }).waitFor()
  await record()
  await recognize.click()
  await panel.waitFor({ state: 'hidden' })
  assert.equal(await composer.inputValue(), 'Это тестовая расшифровка Budz.')
  assert.equal(submitted.length, 1)
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
  assert.equal(delivered, 2)
  console.log('Verified explicit transcription, automatic draft insertion, overflow, Enter, retry, timeout, cancelled responses and temporary chat.')
} finally { await browser.close() }
