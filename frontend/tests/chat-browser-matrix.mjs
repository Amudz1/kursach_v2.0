import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { chromium, devices, firefox, webkit } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const targets = [
  ['Chrome desktop engine', chromium, {}],
  ['Firefox desktop engine', firefox, {}],
  ['WebKit desktop engine', webkit, {}],
  ['Android viewport and input emulation', chromium, devices['Pixel 7']],
  ['iPhone viewport and input emulation', webkit, devices['iPhone 13']],
]

for (const [name, engine, device] of targets) {
  const browser = await engine.launch()
  const context = await browser.newContext({ viewport: { width: 1280, height: 840 }, ...device, baseURL })
  try {
    const username = `matrix-${Math.random().toString(36).slice(2, 12)}`
    const password = 'Budz-regression-2026'
    const signup = await context.request.post('/api/v1/auth/register', { data: {
      username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
    } })
    assert.equal(signup.status(), 201)
    const grant = await signup.json()
    const headers = { Authorization: `Bearer ${grant.accessToken}` }
    const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
    assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
    await context.route('**/api/v1/auth/refresh', route => route.fulfill({ status: 200, json: grant }))
    await context.addInitScript(() => {
      const supported = ['audio/webm;codecs=opus', 'audio/mp4;codecs=mp4a.40.2', 'audio/mp4', 'audio/ogg;codecs=opus']
      window.budzAudioSupport = {
        secure: window.isSecureContext, microphone: typeof navigator.mediaDevices?.getUserMedia === 'function',
        formats: supported.filter(format => typeof MediaRecorder !== 'undefined' && MediaRecorder.isTypeSupported(format)),
        calls: 0,
      }
      if (window.budzAudioSupport.microphone) navigator.mediaDevices.getUserMedia = () => {
        window.budzAudioSupport.calls++
        return Promise.reject(new DOMException('Simulated denial', 'NotAllowedError'))
      }
    })
    const page = await context.newPage()
    await page.goto('/chats')
    const composer = page.getByRole('textbox', { name: 'Сообщение для Budz' })
    const phrase = 'Проверка обычного сообщения'
    await composer.fill(phrase)
    if (device.isMobile) {
      await page.getByRole('button', { name: 'Отправить', exact: true }).click()
    } else {
      const ctrlEnterAccepted = await composer.evaluate(el => el.dispatchEvent(new KeyboardEvent('keydown', {
        key: 'Enter', ctrlKey: true, bubbles: true, cancelable: true,
      })))
      assert.equal(ctrlEnterAccepted, true)
      await composer.press('Control+Enter')
      assert.equal(new URL(page.url()).pathname, '/chats')
      await composer.fill(phrase)
      await composer.press('Enter')
    }
    await page.waitForURL(/\/chats\/[a-z\d-]+/)
    const chat = page.url()
    await page.locator('.message.user').getByText(phrase, { exact: true }).waitFor()
    await page.getByRole('button', { name: 'Меню аккаунта', exact: true }).click()
    await page.getByRole('menuitemradio', { name: 'Фокус', exact: true }).click()
    await page.locator('.focus-chat').waitFor()
    assert.equal(page.url(), chat)
    await page.locator('.message.user').getByText(phrase, { exact: true }).waitFor()
    await composer.fill('Продолжение в «Фокусе»')
    const mic = page.getByRole('button', { name: 'Записать голос', exact: true })
    const support = await page.evaluate(() => window.budzAudioSupport)
    assert.equal(support.calls, 0)
    await mic.click()
    const expected = support.secure && support.microphone && support.formats.length
      ? 'Доступ к микрофону запрещён.' : support.secure ? 'Запись голоса недоступна в этом браузере.' : 'Для записи голоса откройте Budz по HTTPS.'
    await page.getByRole('group', { name: 'Запись голоса' }).getByRole('alert').filter({ hasText: expected }).waitFor()
    assert.equal(await composer.inputValue(), 'Продолжение в «Фокусе»')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    if (name === 'Chrome desktop engine') {
      await page.locator('.chat-menu summary').click()
      await page.getByRole('link', { name: 'Экспорт диалога', exact: true }).click()
      await page.waitForURL(url => url.pathname === '/data' && Boolean(url.searchParams.get('chat')))
      const [archive] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: 'Скачать JSON' }).click()])
      const data = JSON.parse(await readFile(await archive.path(), 'utf-8'))
      assert.equal(data.conversations[0].messages[0].content, phrase)
    }
    console.log(`${name} ${browser.version()}: text, saved selection, microphone error; formats ${support.formats.join(', ') || 'unavailable'}`)
  } finally { await browser.close() }
}
