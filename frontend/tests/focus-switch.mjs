import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const context = await browser.newContext({
  baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080',
  viewport: { width: 1280, height: 850 },
  reducedMotion: 'reduce',
})
const page = await context.newPage()
const username = `switch-${Date.now().toString(36)}`
const password = 'Budz-focus-switch-2026'

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  const project = await context.request.post('/api/v1/workspaces', { headers, data: { name: 'Программирование', instructions: '', materials: '' } })
  const workspaceId = (await project.json()).id
  const archived = Array.from({ length: 40 }, (_, index) => ({
    sender: index % 2 ? 'assistant' : 'human',
    text: `Сообщение ${index + 1}\n\n${'Содержимое переписки. '.repeat(10)}`,
    created_at: new Date(Date.UTC(2025, 1, 15, 12, index)).toISOString(),
  }))
  const imported = await context.request.post('/api/v1/data/import', { headers, multipart: {
    workspaceId, file: { name: 'thread.json', mimeType: 'application/json',
      buffer: Buffer.from(JSON.stringify([{ name: 'Замена интерфейса', uuid: username, chat_messages: archived }])) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const chatId = (await imported.json()).chatIds[0]
  let failures = 0
  let sends = 0
  page.on('request', request => {
    if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith(`/${chatId}/messages`)) sends++
  })
  await page.route('**/api/v1/me/interface', async route => {
    if (route.request().method() !== 'PUT') return route.continue()
    if (failures++ === 0) return route.fulfill({ status: 503, json: { title: 'Проверка повтора переключения' } })
    return route.continue()
  })

  await page.goto(`/chats/${chatId}?workspace=${workspaceId}`)
  const address = page.url()
  const current = page.locator('.workspace[data-interface="current"]')
  const focus = page.locator('.workspace[data-interface="focus"]')
  const view = page.locator('.workspace')
  const transcript = view.locator('.transcript')
  const draft = view.getByRole('textbox', { name: 'Сообщение для Budz' })
  await transcript.locator('article').first().waitFor()
  await draft.fill('Черновик перед сменой интерфейса')
  await view.getByLabel('Выбор файла').setInputFiles({ name: 'заметки.txt', mimeType: 'text/plain', buffer: Buffer.from('Материалы') })
  await view.getByText('заметки.txt', { exact: true }).waitFor()
  const reading = await transcript.evaluate(el => {
    el.scrollTop = 480
    const line = el.getBoundingClientRect().top + 8
    const item = [...el.querySelectorAll('[data-reading-message]')].find(message => message.getBoundingClientRect().bottom > line)
    return { id: item.id, offset: Math.max(0, Math.round(line - item.getBoundingClientRect().top)) }
  })
  const accountMenu = page.getByRole('button', { name: 'Меню аккаунта', exact: true })
  const quick = async label => {
    await accountMenu.click()
    await page.getByRole('group', { name: 'Интерфейс' }).getByRole('menuitemradio', { name: label, exact: true }).click()
  }
  await quick('Фокус')
  const failure = page.getByRole('alert').filter({ hasText: 'Не удалось сменить интерфейс.' })
  await failure.waitFor()
  assert.ok(await current.isVisible())
  await failure.getByRole('button', { name: 'Повторить', exact: true }).click()
  await focus.waitFor()
  assert.equal(page.url(), address)
  assert.equal(await draft.inputValue(), 'Черновик перед сменой интерфейса')
  await focus.getByText('заметки.txt', { exact: true }).waitFor()
  await focus.getByRole('heading', { name: 'Замена интерфейса', exact: true }).waitFor()
  await focus.locator('.chat-scope-name').filter({ hasText: 'Программирование' }).waitFor()
  await page.waitForFunction(({ id, offset }) => {
    const panel = document.querySelector('.focus-chat .transcript')
    const item = document.getElementById(id)
    return item && Math.abs(panel.getBoundingClientRect().top + 8 - item.getBoundingClientRect().top - offset) <= 3
  }, reading)
  assert.equal(sends, 0)

  await focus.getByRole('button', { name: 'Отправить', exact: true }).click()
  const streaming = focus.locator('.message.assistant .streaming').last()
  await streaming.waitFor()
  const responseId = await streaming.locator('xpath=ancestor::article').getAttribute('id')
  await quick('Текущий Budz')
  await current.waitFor()
  assert.equal(page.url(), address)
  const response = current.locator(`#${responseId}`)
  await response.waitFor()
  const stop = current.getByRole('button', { name: 'Остановить', exact: true })
  assert.ok(await stop.isVisible(), 'The same streaming response must remain stoppable after switching')
  assert.ok(await transcript.evaluate(el => el.scrollHeight - el.scrollTop - el.clientHeight <= 3))
  await stop.click()
  await response.getByText('Генерация остановлена.', { exact: true }).waitFor()
  assert.equal(sends, 1)

  await page.setViewportSize({ width: 320, height: 780 })
  await page.getByRole('button', { name: 'Открыть меню' }).click()
  const phoneMenu = page.getByRole('dialog', { name: 'Меню', exact: true })
  const mobileOptions = phoneMenu.getByRole('group', { name: 'Интерфейс' })
  assert.equal(await mobileOptions.getByRole('button', { name: 'Текущий Budz', exact: true }).getAttribute('aria-pressed'), 'true')
  await mobileOptions.getByRole('button', { name: 'Фокус', exact: true }).click()
  await focus.waitFor()
  await quick('Текущий Budz')
  await current.waitFor()
  assert.equal(page.url(), address)
  assert.equal(sends, 1)
  console.log('Verified quick switching: retry, stable address, reading, draft, files, same live generation and mobile menu.')
} finally { await browser.close() }
