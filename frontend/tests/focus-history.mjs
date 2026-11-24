import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const context = await browser.newContext({
  baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080',
  viewport: { width: 1280, height: 850 },
})
const page = await context.newPage()
const username = `history-${Date.now().toString(36)}`
const password = 'Budz-history-test-2026'

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)
  const project = await context.request.post('/api/v1/workspaces', { headers, data: { name: 'Поездка', instructions: '', materials: '' } })
  assert.equal(project.status(), 201)
  const workspaceId = (await project.json()).id
  const createChat = async (title, projectId) => {
    const response = await context.request.post('/api/v1/chats', { headers, data: { workspaceId: projectId } })
    assert.equal(response.status(), 201)
    const { id } = await response.json()
    assert.equal((await context.request.patch(`/api/v1/chats/${id}`, { headers, data: { title } })).status(), 204)
    return id
  }
  const itinerary = await createChat('Маршрут на выходные', workspaceId)
  const notes = 'Содержимое уже созданной переписки'
  const archive = JSON.stringify([{ name: 'Черновые заметки', uuid: username,
    chat_messages: [{ sender: 'human', text: notes, created_at: new Date().toISOString() }],
  }])
  const imported = await context.request.post('/api/v1/data/import', { headers, multipart: {
    file: { name: 'history.json', mimeType: 'application/json', buffer: Buffer.from(archive) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const [draft] = (await imported.json()).chatIds

  await page.goto('/chats')
  const sidebar = page.locator('#chat-history')
  const list = sidebar.getByRole('navigation', { name: 'История диалогов' })
  await list.getByRole('link', { name: 'Маршрут на выходные Поездка' }).waitFor()
  assert.equal((await sidebar.boundingBox()).width, 256)
  await page.getByRole('button', { name: 'Скрыть список диалогов' }).click()
  await page.reload()
  const showHistory = page.getByRole('button', { name: 'Показать список диалогов' })
  await showHistory.waitFor()
  assert.equal(await sidebar.isVisible(), false)
  await showHistory.click()

  const search = sidebar.getByRole('searchbox', { name: 'Поиск по диалогам' })
  await search.fill('выходные')
  await list.getByRole('link', { name: 'Черновые заметки' }).waitFor({ state: 'hidden' })
  await list.getByRole('link', { name: 'Маршрут на выходные Поездка' }).click()
  await page.waitForURL(url => url.pathname === `/chats/${itinerary}` && url.searchParams.get('workspace') === workspaceId)
  assert.match(await list.locator('[aria-current]').innerText(), /Маршрут на выходные\s+Поездка/)
  await search.clear()
  const rowActions = title => sidebar.getByRole('button', { name: `Действия диалога ${title}` })
  await rowActions('Черновые заметки').click()
  await sidebar.getByRole('button', { name: 'Переименовать' }).click()
  const rename = page.getByRole('dialog', { name: 'Переименовать диалог' })
  await rename.getByLabel('Название').fill('Обновлённые заметки')
  await rename.getByRole('button', { name: 'Сохранить' }).click()
  await list.getByRole('link', { name: 'Обновлённые заметки' }).waitFor()
  await sidebar.getByRole('button', { name: 'Закрепить', exact: true }).click()
  await list.getByRole('region', { name: 'Закреплённые' }).getByRole('link', { name: 'Обновлённые заметки' }).waitFor()
  const moveDialog = page.getByRole('dialog', { name: 'Переместить в проект' })
  const destination = moveDialog.getByRole('combobox', { name: 'Проект назначения' })
  const confirmMove = moveDialog.getByRole('button', { name: 'Переместить', exact: true })
  await sidebar.getByRole('button', { name: 'Переместить в проект' }).click()
  assert.ok(await confirmMove.isDisabled())
  await destination.selectOption(workspaceId)
  await confirmMove.click()
  await moveDialog.waitFor({ state: 'hidden' })
  await list.getByRole('link', { name: 'Обновлённые заметки Поездка' }).waitFor()
  assert.equal(new URL(page.url()).pathname, `/chats/${itinerary}`)
  const moved = await (await context.request.get(`/api/v1/chats/${draft}`, { headers })).json()
  assert.equal(moved.workspaceId, workspaceId)
  const messages = await (await context.request.get(`/api/v1/chats/${draft}/messages`, { headers })).json()
  assert.equal(messages.items[0].content, notes)
  await rowActions('Обновлённые заметки').click()
  await sidebar.getByRole('button', { name: 'Удалить', exact: true }).click()
  await page.getByRole('dialog', { name: 'Удалить диалог?' }).getByRole('button', { name: 'Удалить' }).click()
  await list.getByRole('link', { name: 'Обновлённые заметки' }).waitFor({ state: 'hidden' })
  assert.equal((await context.request.get(`/api/v1/chats/${draft}`, { headers })).status(), 404)
  assert.equal(new URL(page.url()).pathname, `/chats/${itinerary}`)

  const projects = sidebar.getByRole('navigation', { name: 'Список проектов' })
  await projects.getByRole('link', { name: 'Поездка' }).click()
  await page.waitForURL(url => url.pathname === '/chats' && url.searchParams.get('workspace') === workspaceId)
  await sidebar.getByRole('button', { name: 'Новый диалог' }).click()
  await page.getByRole('textbox', { name: 'Сообщение для Budz' }).waitFor()

  await page.setViewportSize({ width: 320, height: 850 })
  const historyDialog = page.getByRole('dialog', { name: 'История диалогов' })
  async function openMobileHistory() {
    await showHistory.click()
    await historyDialog.waitFor()
    assert.equal(await page.evaluate(() => document.documentElement.style.overflow), 'hidden')
  }
  async function assertClosed() {
    await historyDialog.waitFor({ state: 'hidden' })
    assert.ok(await showHistory.evaluate(element => document.activeElement === element))
    assert.equal(await page.evaluate(() => document.documentElement.style.overflow), '')
  }
  await openMobileHistory()
  await page.keyboard.press('Shift+Tab')
  assert.ok(await historyDialog.evaluate(element => element.contains(document.activeElement)))
  await page.keyboard.press('Tab')
  assert.ok(await historyDialog.evaluate(element => element.contains(document.activeElement)))
  await page.keyboard.press('Escape')
  await assertClosed()
  await openMobileHistory()
  await historyDialog.getByRole('button', { name: 'Закрыть историю' }).click()
  await assertClosed()
  await openMobileHistory()
  await page.mouse.click(310, 450)
  await assertClosed()
  await openMobileHistory()
  await list.getByRole('link', { name: 'Маршрут на выходные Поездка' }).click()
  await assertClosed()
  await page.waitForURL(url => url.pathname === `/chats/${itinerary}`)
  const composer = page.getByRole('textbox', { name: 'Сообщение для Budz' })
  await composer.fill('Сохранить текст при переносе')
  await page.locator('.chat-menu summary').click()
  await page.getByRole('button', { name: 'Переместить в проект' }).click()
  assert.equal(await destination.inputValue(), workspaceId)
  await destination.selectOption('')
  await confirmMove.click()
  await moveDialog.waitFor({ state: 'hidden' })
  await page.waitForURL(url => url.pathname === `/chats/${itinerary}` && !url.searchParams.has('workspace'))
  assert.equal(await composer.inputValue(), 'Сохранить текст при переносе')

  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'current' } })).status(), 204)
  await page.setViewportSize({ width: 1280, height: 850 })
  await page.reload()
  await page.locator('.workspace[data-interface="current"]').waitFor()
  await rowActions('Маршрут на выходные').click()
  await sidebar.getByRole('button', { name: 'Переместить в проект' }).click()
  await destination.selectOption(workspaceId)
  await confirmMove.click()
  await moveDialog.waitFor({ state: 'hidden' })
  await page.waitForURL(url => url.pathname === `/chats/${itinerary}` && url.searchParams.get('workspace') === workspaceId)
  assert.equal(await composer.inputValue(), 'Сохранить текст при переносе')
  console.log('Verified chat history, mobile focus and moving active or background chats in both interfaces.')
} finally { await browser.close() }
