import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const context = await browser.newContext({ baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080', viewport: { width: 1280, height: 850 } })
const page = await context.newPage()
const username = `quote-${Date.now().toString(36)}`
const password = 'Budz-quote-2026'
const original = 'Первое предложение ответа. Нужное объяснение для цитаты. Конец абзаца.'
const selected = 'Нужное объяснение для цитаты.'
const edited = 'Уточнённый фрагмент'
const question = 'Как это устроено?'

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })
  const owner = (await (await context.request.get('/api/v1/me', { headers })).json()).id
  const imported = await context.request.post('/api/v1/data/import', { headers, multipart: {
    file: { name: 'quote.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify([{
      name: 'Цитирование в Budz', uuid: username, chat_messages: [
        { sender: 'human', text: 'Расскажи подробнее.' }, { sender: 'assistant', text: original },
      ],
    }])) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const chat = (await imported.json()).chatIds[0]
  const key = `budz.draft.${owner}.chat.${chat}`
  const requests = []
  page.on('request', request => {
    if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith(`/chats/${chat}/messages`)) requests.push(request.postData())
  })
  await page.goto(`/chats/${chat}`)
  const answer = page.locator('.message.assistant').filter({ hasText: original })
  const paragraph = answer.getByText(original, { exact: true })
  const quoteButton = answer.getByRole('button', { name: 'Цитировать', exact: true })
  const dialog = page.getByRole('dialog', { name: 'Цитировать ответ' })
  const editor = dialog.getByRole('textbox', { name: 'Фрагмент ответа' })
  const add = dialog.getByRole('button', { name: 'Добавить цитату' })
  const card = page.getByRole('group', { name: 'Цитата к сообщению' })
  const composer = page.getByRole('textbox', { name: 'Сообщение для Budz' })
  await paragraph.evaluate((element, text) => {
    const node = element.firstChild
    const from = node.textContent.indexOf(text)
    const range = document.createRange()
    range.setStart(node, from)
    range.setEnd(node, from + text.length)
    window.getSelection().removeAllRanges()
    window.getSelection().addRange(range)
  }, selected)
  await quoteButton.click()
  await editor.waitFor()
  assert.equal(await editor.inputValue(), selected)
  await editor.fill('x'.repeat(601))
  assert.ok(await add.isDisabled())
  await dialog.getByText(/Сократите фрагмент/).waitFor()
  await editor.fill(edited)
  await add.click()
  await card.getByText(edited, { exact: true }).waitFor()
  assert.ok(await page.getByRole('button', { name: 'Отправить', exact: true }).isDisabled())
  await composer.fill(question)
  assert.equal(requests.length, 0)
  await page.waitForFunction(key => !!JSON.parse(localStorage.getItem(key) ?? '{}').quote, key)
  await page.reload()
  await card.getByText(edited, { exact: true }).waitFor()
  assert.equal(await composer.inputValue(), question)
  await card.getByRole('link', { name: 'К исходному ответу' }).click()
  assert.ok(await answer.evaluate(element => element === document.activeElement && element.classList.contains('message-target')))
  await card.getByRole('button', { name: 'Убрать цитату' }).click()
  await card.waitFor({ state: 'hidden' })
  assert.equal(await composer.inputValue(), question)

  await page.getByRole('button', { name: 'Меню аккаунта' }).click()
  await page.getByRole('menuitemradio', { name: 'Фокус', exact: true }).click()
  await page.locator('.workspace[data-interface="focus"]').waitFor()
  await page.setViewportSize({ width: 320, height: 780 })
  await quoteButton.focus()
  await quoteButton.press('Enter')
  await editor.waitFor()
  assert.equal(await editor.inputValue(), original)
  await editor.fill(edited)
  await add.click()
  assert.equal(await composer.inputValue(), question)
  await composer.fill('a'.repeat(10_000 - `> ${edited}\n\n`.length))
  const send = page.getByRole('button', { name: 'Отправить', exact: true })
  await send.click()
  await page.getByRole('alert').filter({ hasText: /Вопрос и цитата вместе превышают/ }).waitFor()
  assert.equal(requests.length, 0)
  await composer.fill(question)
  await send.click()
  await page.locator('.message.user blockquote').filter({ hasText: edited }).waitFor()
  assert.equal(requests.length, 1)
  const exported = await context.request.get(`/api/v1/data/export?format=json&chatId=${chat}`, { headers })
  const data = await exported.json()
  const exportedText = data.conversations[0].messages.filter(message => message.role === 'user').map(message => message.content.replace(/\r\n/g, '\n'))
  assert.equal(exportedText.filter(content => content === `> ${edited}\n\n${question}`).length, 1)

  await page.evaluate(({ key, id, edited }) => localStorage.setItem(key,
    JSON.stringify({ text: 'Черновик', quote: { text: edited, messageId: id }, updatedAt: Date.now() })),
    { key, id: randomUUID(), edited })
  await page.reload()
  await card.getByText(edited, { exact: true }).waitFor()
  await card.getByRole('link', { name: 'К исходному ответу' }).click()
  await card.getByText('Источник недоступен. Текст цитаты сохранён.', { exact: true }).waitFor()
  assert.equal(await composer.inputValue(), 'Черновик')
  console.log('Verified quote selection, keyboard, draft restoration, length limits, Markdown export and unavailable source.')
} finally { await browser.close() }
