import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { join } from 'node:path'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const adminPassword = process.env.BUDZ_TEST_ADMIN_PASSWORD
const captureDir = process.env.BUDZ_CAPTURE_DIR
assert.ok(adminPassword, 'Set BUDZ_TEST_ADMIN_PASSWORD to the demo administrator password.')

const browser = await chromium.launch()
const userContext = await browser.newContext({ baseURL, viewport: { width: 1440, height: 900 } })
const adminContext = await browser.newContext({ baseURL })
const page = await userContext.newPage()
const admin = await adminContext.newPage()
const errors = []
for (const target of [page, admin]) target.on('pageerror', error => errors.push(error.message))
const username = `help-${Date.now().toString(36)}`
const password = 'Budz-help-browser-test-2026'
const question = 'Как найти закладку в беседе?'
const reply = 'Откройте оглавление и выберите раздел «Закладки».'
const role = (target, type, name) => target.getByRole(type, { name, exact: true })

async function capture(target, name) {
  if (!captureDir) return
  await mkdir(captureDir, { recursive: true })
  await target.screenshot({ path: join(captureDir, name + '.png'), animations: 'disabled' })
}

async function noOverflow(target) {
  assert.ok(await target.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
}

async function completeOnboarding(context, accessToken) {
  const headers = { Authorization: `Bearer ${accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  if (!profile.onboardingCompleted)
    assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  return headers
}

try {
  await page.goto('/login')
  await role(page, 'link', 'Правовая информация').click()
  await page.waitForURL('**/help/documents')
  assert.equal(await page.getByText('Демонстрационный шаблон', { exact: true }).count(), 2)
  for (const title of ['Пользовательское соглашение', 'Политика конфиденциальности']) {
    await role(page, 'link', title).click()
    await role(page, 'heading', title).waitFor()
    await page.reload()
    await role(page, 'heading', title).waitFor()
    await page.getByText('Демонстрационный шаблон', { exact: true }).waitFor()
    if (title.startsWith('Политика')) await capture(page, 'help-privacy')
    await role(page, 'link', 'Все документы').click()
  }
  await role(page, 'link', 'Возможности').click()
  await role(page, 'heading', 'Возможности Budz').waitFor()
  const quick = role(page, 'navigation', 'Быстрая помощь')
  await role(quick, 'link', 'Микрофон или распознавание недоступны').click()
  await page.waitForURL(url => url.hash === '#microphone')
  await page.locator('#microphone').getByText(/откройте разрешения сайта/).waitFor()
  await page.locator('#voice').getByText(/автоматически появится в черновике/).waitFor()
  await page.locator('#interface').getByText(/Применить интерфейс/).waitFor()
  await page.locator('#drafts').getByText(/Во временном диалоге/).waitFor()
  assert.match(await role(page.locator('#voice-data'), 'link', 'Условия обработки данных OpenAI').getAttribute('href'),
    /^https:\/\/developers\.openai\.com\//)
  await role(page, 'link', 'Поддержка').click()
  await role(page, 'link', 'Войти, чтобы написать').waitFor()
  assert.equal(await page.getByLabel('Сообщение в поддержку').count(), 0)

  const registration = await userContext.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(registration.status(), 201)
  const headers = await completeOnboarding(userContext, (await registration.json()).accessToken)
  const imported = await userContext.request.post('/api/v1/data/import', { headers, multipart: {
    file: { name: 'chat.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({ conversations: [{
      title: 'Проверка справки', messages: [{ role: 'user', content: 'Где справка?' }, { role: 'assistant', content: 'Откройте меню.' }],
    }] })) },
  } })
  assert.ok(imported.ok())
  const chat = `/chats/${(await imported.json()).chatIds[0]}`

  await page.goto('/profile')
  await role(page, 'link', 'Правовая информация').waitFor()
  await page.goto(chat)
  await role(page, 'heading', 'Проверка справки').waitFor()
  assert.equal(await role(page, 'link', 'Правовая информация').count(), 0)
  await role(page, 'button', 'Меню аккаунта').click()
  await role(page, 'menuitem', 'Справка').click()
  await role(page, 'heading', 'Возможности Budz').waitFor()
  await capture(page, 'help-desktop')

  await page.setViewportSize({ width: 320, height: 780 })
  await page.goto(chat)
  await page.getByLabel('Сообщение для Budz').waitFor()
  assert.equal(await role(page, 'link', 'Правовая информация').count(), 0)
  await noOverflow(page)
  await role(page, 'button', 'Открыть меню').click()
  await role(role(page, 'dialog', 'Меню'), 'link', 'Справка').click()
  await role(page, 'link', 'Поддержка').click()
  const draft = page.getByLabel('Сообщение в поддержку', { exact: true })
  await draft.fill(question)
  const supportUrl = '**/api/v1/me/support/messages*'
  const reject = route => route.request().method() === 'POST'
    ? route.fulfill({ status: 503, json: { title: 'Сервис временно недоступен.' } }) : route.continue()
  await page.route(supportUrl, reject)
  await draft.press('Tab')
  await page.keyboard.press('Enter')
  await page.getByRole('alert').getByText('Сервис временно недоступен.').waitFor()
  assert.equal(await draft.inputValue(), question)
  await page.unroute(supportUrl, reject)
  await role(page, 'button', 'Отправить').click()
  await page.getByText(question, { exact: true }).waitFor()

  const login = await adminContext.request.post('/api/v1/auth/login', { data: { login: 'demo-admin', password: adminPassword } })
  assert.equal(login.status(), 200)
  await completeOnboarding(adminContext, (await login.json()).accessToken)
  await admin.goto('/admin')
  await role(admin, 'link', 'Поддержка').click()
  await admin.getByRole('navigation', { name: 'Переписки поддержки' }).getByRole('link', { name: new RegExp(username) }).click()
  await admin.getByText(question, { exact: true }).waitFor()
  await admin.getByLabel('Ответ пользователю', { exact: true }).fill(reply)
  await role(admin, 'button', 'Отправить').click()
  await admin.getByText(reply, { exact: true }).waitFor()
  await capture(admin, 'help-admin')

  await page.bringToFront()
  await page.getByText(reply, { exact: true }).waitFor({ timeout: 15000 })
  await noOverflow(page)
  await capture(page, 'help-mobile')
  await role(page, 'button', 'Меню аккаунта').click()
  await role(page, 'menuitem', 'Выйти').click()
  await role(page, 'link', 'Войти, чтобы написать').click()
  await page.getByLabel('Логин или email').fill(username)
  await page.getByLabel('Пароль', { exact: true }).fill(password)
  await role(page, 'button', 'Войти').click()
  await page.waitForURL('**/help/support')
  await page.getByText(reply, { exact: true }).waitFor()
  assert.deepEqual(errors, [])
  console.log('Verified public documents, desktop and mobile help navigation, draft recovery, support reply and history after login.')
} finally {
  await browser.close()
}
