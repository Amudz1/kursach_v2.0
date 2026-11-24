import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { join } from 'node:path'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch()
const context = await browser.newContext({ baseURL, viewport: { width: 1440, height: 925 } })
const page = await context.newPage()
const username = `picker-${Date.now().toString(36)}`
const password = 'Budz-interface-picker-2026'
const captured = process.env.BUDZ_CAPTURE_DIR
const messages = []
page.on('request', request => {
  if (request.method() === 'POST' && /\/api\/v1\/chats(?:\/|$)/.test(request.url())) messages.push(request.url())
})

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  await page.goto('/appearance')
  const picker = page.getByRole('region', { name: 'Интерфейс' })
  const current = picker.getByRole('radio', { name: 'Текущий Budz', exact: true })
  const focus = picker.getByRole('radio', { name: 'Фокус', exact: true })
  await current.waitFor({ state: 'attached' })
  assert.equal(await current.isChecked(), true)
  await current.press('ArrowRight')
  assert.equal(await focus.isChecked(), true)
  await picker.getByRole('radio', { name: 'Первый экран', exact: true }).check()
  assert.equal(await picker.locator('[data-preview="start"]').count(), 2)
  await picker.getByRole('radio', { name: 'Открытый диалог', exact: true }).check()
  assert.equal(await picker.locator('[data-preview="chat"]').count(), 2)

  const endpoint = '**/api/v1/me/interface'
  await page.route(endpoint, route => route.fulfill({ status: 503, json: { title: 'Сервис выбора недоступен.' } }))
  await picker.getByRole('button', { name: 'Применить интерфейс' }).click()
  await picker.getByText('Сервис выбора недоступен.', { exact: true }).waitFor()
  await picker.getByText('Сейчас действует «Текущий Budz»', { exact: true }).waitFor()
  await page.unroute(endpoint)
  await picker.getByRole('button', { name: 'Повторить сохранение интерфейса' }).click()
  await picker.getByText('Сейчас действует «Фокус»', { exact: true }).waitFor()
  if (captured) {
    await mkdir(captured, { recursive: true })
    await picker.screenshot({ path: join(captured, 'interface-picker.png'), animations: 'disabled' })
  }

  const preview = picker.locator('.interface-preview').first()
  const color = await preview.evaluate(element => getComputedStyle(element).backgroundColor)
  await page.getByRole('radio', { name: 'Тёмная', exact: true }).check()
  assert.notEqual(await preview.evaluate(element => getComputedStyle(element).backgroundColor), color)
  await page.getByRole('button', { name: 'Сохранить', exact: true }).click()
  await page.getByText('Оформление сохранено.', { exact: true }).waitFor()

  const [exported] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: 'Экспорт темы' }).click()])
  await page.getByRole('button', { name: 'Сбросить к стандартным' }).click()
  await page.getByText('Стандартное оформление восстановлено.', { exact: true }).waitFor()
  await page.locator('.appearance-page input[type="file"]').setInputFiles(await exported.path())
  await page.getByText('Тема загружена в предпросмотр.', { exact: true }).waitFor()
  await page.getByRole('button', { name: 'Сохранить', exact: true }).click()
  await page.getByText('Оформление сохранено.', { exact: true }).waitFor()
  await picker.getByText('Сейчас действует «Фокус»', { exact: true }).waitFor()

  await page.setViewportSize({ width: 320, height: 850 })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
  if (captured) await picker.screenshot({ path: join(captured, 'interface-picker-320.png'), animations: 'disabled' })
  assert.deepEqual(messages, [])
  await page.goto('/chats')
  await page.locator('[data-interface="focus"]').waitFor()
  console.log('Verified keyboard selection, preview, failed save and retry, theme import/reset and mobile layout.')
} finally { await browser.close() }
