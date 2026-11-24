import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const context = await browser.newContext({
  baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080',
  viewport: { width: 1440, height: 850 },
})
const page = await context.newPage()
const username = `composer-${Date.now().toString(36)}`
const password = 'Budz-focus-composer-2026'

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)

  const requests = []
  await page.route(/\/api\/v1\/chats\/[^/]+\/messages(?:\?|$)/, async route => {
    if (route.request().method() !== 'POST') return route.continue()
    requests.push(route.request().postData()?.match(/name="requestId"\r?\n\r?\n([^\r\n]+)/)?.[1])
    if (requests.length === 1) {
      return route.fulfill({ status: 503, json: { title: 'Проверка сохранения черновика' } })
    }
    return route.continue()
  })

  await page.goto('/chats')
  const focus = page.locator('.focus-chat')
  const field = focus.getByRole('textbox', { name: 'Сообщение для Budz' })
  const composer = focus.locator('.composer')
  const send = focus.getByRole('button', { name: 'Отправить', exact: true })
  await field.waitFor()
  assert.ok((await composer.boundingBox()).width <= 760)
  assert.equal(await composer.evaluate(el => getComputedStyle(el).borderRadius), '24px')
  assert.ok(await send.isDisabled())
  await focus.getByRole('button', { name: 'Настройки модели' }).click()
  assert.ok(await focus.getByRole('combobox', { name: 'Модель' }).evaluate(el => el === document.activeElement))
  await focus.locator('.chat-menu summary').click()

  await field.fill('Нужен план')
  await field.press('Shift+Enter')
  await field.press('End')
  await page.keyboard.insertText('В два этапа')
  assert.equal(await field.inputValue(), 'Нужен план\nВ два этапа')
  const compositionEnter = await field.evaluate(el => el.dispatchEvent(new KeyboardEvent('keydown', {
    key: 'Enter', isComposing: true, bubbles: true, cancelable: true,
  })))
  assert.equal(compositionEnter, true)
  assert.equal(requests.length, 0)
  await field.press('Enter')
  await focus.getByRole('alert').filter({ hasText: 'Проверка сохранения черновика' }).waitFor()
  assert.equal(await field.inputValue(), 'Нужен план\nВ два этапа')
  await field.press('Enter')
  await page.waitForURL(/\/chats\/[a-z\d-]+/)
  const stop = focus.getByRole('button', { name: 'Остановить', exact: true })
  await stop.click()
  await focus.getByText('Генерация остановлена.', { exact: true }).waitFor()
  assert.equal(requests.length, 2)
  assert.ok(requests[0])
  assert.equal(requests[1], requests[0])

  await field.fill(Array.from({ length: 1000 }, () => 'Ещё строка').join('\n').slice(0, 9400))
  await focus.getByText('10 000 символов', { exact: false }).waitFor()
  assert.ok(await field.evaluate(el => el.clientHeight <= 142 && el.scrollHeight > el.clientHeight))
  assert.ok((await send.boundingBox()).y > (await field.boundingBox()).y)
  const captureDir = process.env.BUDZ_CAPTURE_DIR
  if (captureDir) {
    await mkdir(captureDir, { recursive: true })
    await page.screenshot({ path: `${captureDir}/focus-composer-desktop.png` })
  }

  await page.setViewportSize({ width: 320, height: 720 })
  await field.fill('На телефоне')
  await field.press('Enter')
  assert.equal(await field.inputValue(), 'На телефоне\n')
  assert.equal(requests.length, 2)
  assert.equal(await composer.evaluate(el => getComputedStyle(el).borderRadius), '18px')
  assert.equal(await field.getAttribute('enterkeyhint'), 'enter')
  const sendSize = await send.boundingBox()
  assert.ok(sendSize.width >= 44 && sendSize.height >= 44)
  await field.evaluate(el => {
    el.focus()
    Object.defineProperty(window.visualViewport, 'height', { configurable: true, value: 340 })
    window.visualViewport.dispatchEvent(new Event('resize'))
  })
  await page.waitForFunction(() => document.querySelector('.focus-chat .send-button')?.getBoundingClientRect().bottom <= 341)
  if (captureDir) await page.screenshot({ path: `${captureDir}/focus-composer-320.png` })
  await page.evaluate(() => {
    delete window.visualViewport.height
    window.visualViewport.dispatchEvent(new Event('resize'))
  })
  await page.setViewportSize({ width: 640, height: 320 })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
  await send.click()
  await focus.getByRole('button', { name: 'Остановить', exact: true }).waitFor()
  assert.equal(requests.length, 3)
  console.log('Verified Focus composer: Enter, Shift+Enter, IME, failed retry, stop, long draft and mobile viewport.')
} finally { await browser.close() }
