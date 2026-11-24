import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch()
const context = await browser.newContext({ baseURL, viewport: { width: 320, height: 850 } })
const page = await context.newPage()
const username = `nav-${Date.now().toString(36)}`
const password = 'Budz-focus-nav-2026'

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)

  await page.goto('/chats')
  const bar = page.locator('.focus-topbar')
  await bar.waitFor()
  assert.equal((await bar.boundingBox()).height, 56)
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
  const history = bar.getByRole('button', { name: 'Показать список диалогов' })
  await history.click()
  const dialog = page.getByRole('dialog', { name: 'История диалогов' })
  await dialog.waitFor()
  await page.keyboard.press('Escape')
  await dialog.waitFor({ state: 'hidden' })
  await page.locator('.chat-menu > summary').click()
  await page.getByLabel('Модель', { exact: true }).waitFor()

  const openMenu = () => bar.getByRole('button', { name: 'Меню аккаунта' }).click()
  await openMenu()
  assert.equal(await bar.getByRole('menuitem', { name: 'Администрирование' }).count(), 0)
  await bar.getByRole('menuitem', { name: 'Проекты', exact: true }).click()
  await page.waitForURL(url => url.pathname === '/workspaces')
  for (const [label, path] of [
    ['Персонализация', '/personalization'], ['Память', '/memory'], ['Тарифы', '/plans'], ['Профиль', '/profile'],
    ['Данные', '/data'], ['Справка', '/help'], ['Оформление', '/appearance'],
  ]) {
    await openMenu()
    await bar.getByRole('menuitem', { name: label, exact: true }).click()
    await page.waitForURL(url => url.pathname === path)
    await bar.waitFor()
  }
  const light = await bar.evaluate(element => getComputedStyle(element).backgroundColor)
  await page.getByRole('radio', { name: 'Тёмная', exact: true }).check()
  assert.notEqual(await bar.evaluate(element => getComputedStyle(element).backgroundColor), light)
  await page.goto('/admin')
  await page.waitForURL(url => url.pathname === '/chats')
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))

  const guest = await browser.newContext({ baseURL })
  try {
    const legal = await guest.newPage()
    await legal.goto('/help/documents/terms')
    await legal.getByRole('heading', { name: 'Пользовательское соглашение' }).waitFor()
    await legal.goto('/login')
    await legal.getByRole('link', { name: 'Правовая информация' }).waitFor()
  } finally { await guest.close() }

  console.log('Verified Focus navigation, mobile history, color scheme, role restriction and public legal links.')
} finally { await browser.close() }
