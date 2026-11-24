import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const context = await browser.newContext({
  baseURL,
  viewport: { width: 1280, height: 790 },
  reducedMotion: 'reduce',
})
const page = await context.newPage()
const username = `links-${Date.now().toString(36)}`
const password = 'Budz-focus-reading-2026'
const firstQuestion = 'Уникальный вопрос из начала'
const lastAnswer = 'Ответ для сохранённой закладки'

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  const setInterface = async value => {
    assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: value } })).status(), 204)
  }
  await setInterface('focus')
  const project = await context.request.post('/api/v1/workspaces', { headers, data: { name: 'Исследование', instructions: '', materials: '' } })
  const workspaceId = (await project.json()).id
  const archived = Array.from({ length: 64 }, (_, index) => ({
    sender: index % 2 ? 'assistant' : 'human',
    text: index === 0 ? firstQuestion : index === 63 ? lastAnswer : `Запись №${index}\n\n${'Фрагмент истории. '.repeat(8)}`,
    created_at: new Date(Date.UTC(2025, 1, 15, 12, index)).toISOString(),
  }))
  const imported = await context.request.post('/api/v1/data/import', { headers, multipart: {
    workspaceId, file: { name: 'reading.json', mimeType: 'application/json',
      buffer: Buffer.from(JSON.stringify([{ name: 'Длинная беседа', uuid: username, chat_messages: archived }])) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const chatId = (await imported.json()).chatIds[0]
  const path = `/chats/${chatId}?workspace=${workspaceId}`

  await page.goto(path)
  const workspace = page.locator('.workspace')
  const messages = workspace.locator('.transcript')
  const recent = messages.locator('article').filter({ hasText: lastAnswer })
  await recent.getByRole('button', { name: 'Добавить закладку' }).click()
  await recent.getByRole('button', { name: 'Убрать закладку' }).waitFor()
  const lastId = await recent.getAttribute('data-reading-message')
  const outline = page.getByRole('dialog', { name: 'Оглавление', exact: true })
  const openOutline = () => page.getByRole('button', { name: 'Оглавление', exact: true }).click()
  const first = messages.locator('article').filter({ hasText: firstQuestion })
  assert.equal(await first.count(), 0)
  await openOutline()
  await outline.getByRole('button', { name: firstQuestion }).click()
  await first.waitFor()
  assert.ok(await first.evaluate(el => el.classList.contains('message-target') && document.activeElement === el))
  const firstId = await first.getAttribute('data-reading-message')
  assert.equal(await recent.count(), 0)
  await openOutline()
  await outline.getByRole('button', { name: lastAnswer }).click()
  await recent.waitFor()
  assert.ok(await recent.evaluate(el => el.classList.contains('message-target') && document.activeElement === el))

  await workspace.locator('.chat-menu summary').click()
  await workspace.getByRole('button', { name: 'Поиск по беседе', exact: true }).click()
  const search = page.getByRole('dialog', { name: 'Поиск по беседе', exact: true })
  await search.getByRole('searchbox', { name: 'Слово или фраза' }).fill(firstQuestion)
  await search.getByRole('button', { name: new RegExp(firstQuestion) }).click()
  await first.waitFor()
  assert.ok(await first.evaluate(el => document.activeElement === el))
  await page.waitForFunction(expected => {
    return Object.keys(localStorage).filter(key => key.startsWith('budz.reading.')).some(key =>
      JSON.parse(localStorage.getItem(key)).some(item => item.messageId === expected))
  }, firstId)
  const start = `/chats?workspace=${workspaceId}`
  await page.goto(start)
  const resume = workspace.locator('a.resume-chat')
  await resume.waitFor()
  assert.match(await resume.getAttribute('href'), new RegExp(`at=${firstId}`))
  await setInterface('current')
  await page.reload()
  await page.locator('.workspace[data-interface="current"]').waitFor()
  await resume.click()
  await first.waitFor()
  await page.waitForFunction(id => document.activeElement?.id === `message-${id}`, firstId)

  await setInterface('focus')
  const unavailableId = randomUUID()
  await page.goto(`${path}&at=${unavailableId}`)
  await page.locator('.workspace[data-interface="focus"]').waitFor()
  const unavailable = workspace.getByRole('alert').filter({ hasText: 'Сообщение недоступно.' })
  await unavailable.waitFor()
  await recent.waitFor()
  await unavailable.getByRole('button', { name: 'Повторить поиск' }).click()
  assert.ok(await search.getByRole('searchbox').evaluate(el => document.activeElement === el))
  await page.keyboard.press('Escape')
  await unavailable.getByRole('button', { name: 'Вернуться к беседе' }).click()
  await page.waitForURL(url => !url.searchParams.has('at'))
  await unavailable.waitFor({ state: 'hidden' })
  assert.equal(await recent.getAttribute('data-reading-message'), lastId)
  assert.ok(await messages.evaluate(el => document.activeElement === el))

  const other = await browser.newContext({ baseURL })
  try {
    const anotherUser = `other-${Date.now().toString(36)}`
    const another = await other.request.post('/api/v1/auth/register', { data: {
      username: anotherUser, email: `${anotherUser}@demo.invalid`, password, passwordConfirmation: password,
    } })
    assert.equal(another.status(), 201)
    const otherHeaders = { Authorization: `Bearer ${(await another.json()).accessToken}` }
    assert.equal((await other.request.get(`/api/v1/chats/${chatId}/messages?around=${firstId}`, { headers: otherHeaders })).status(), 404)
  } finally { await other.close() }
  console.log('Verified Focus reading: search, outline, bookmarks outside current page, resume across interfaces and missing-target recovery.')
} finally { await browser.close() }
