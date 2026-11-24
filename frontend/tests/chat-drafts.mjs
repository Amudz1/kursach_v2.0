import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const context = await browser.newContext({ baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080' })
const page = await context.newPage()
const password = 'Budz-drafts-2026'

async function register() {
  const username = `draft-${Math.random().toString(36).slice(2, 12)}`
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  const owner = (await (await context.request.get('/api/v1/me', { headers })).json()).id
  return { headers, owner }
}

try {
  const { headers, owner } = await register()
  const project = await context.request.post('/api/v1/workspaces', { headers, data: { name: 'Учёба', instructions: '', materials: '' } })
  const workspace = (await project.json()).id
  async function chat(title, temporary = false) {
    const response = await context.request.post('/api/v1/chats', { headers, data: { temporary } })
    assert.equal(response.status(), 201)
    const id = (await response.json()).id
    if (!temporary) assert.equal((await context.request.patch(`/api/v1/chats/${id}`, { headers, data: { title } })).status(), 204)
    return id
  }
  const first = await chat('Первый чат')
  const second = await chat('Второй чат')
  const temporary = await chat('', true)
  const firstKey = `budz.draft.${owner}.chat.${first}`
  const secondKey = `budz.draft.${owner}.chat.${second}`
  const temporaryKey = `budz.draft.${owner}.chat.${temporary}`
  const projectKey = `budz.draft.${owner}.new.${workspace}`
  const composer = target => target.getByRole('textbox', { name: 'Сообщение для Budz' })
  const waitText = (target, text) => target.waitForFunction(value => document.querySelector('#composer')?.value === value, text)
  const waitSaved = (target, key, text) => target.waitForFunction(({ key, text }) => {
    const saved = localStorage.getItem(key)
    return saved && JSON.parse(saved).text === text
  }, { key, text })

  await page.goto(`/chats/${first}`)
  await composer(page).fill('Черновик первого')
  await page.getByLabel('Выбор файла').setInputFiles({ name: 'private-name.txt', mimeType: 'text/plain', buffer: Buffer.from('private-content') })
  await waitSaved(page, firstKey, 'Черновик первого')
  assert.doesNotMatch(await page.evaluate(key => localStorage.getItem(key), firstKey), /private-(name|content)/)
  await page.getByRole('link', { name: 'Второй чат', exact: true }).click()
  await waitText(page, '')
  await composer(page).fill('Черновик второго')
  await waitSaved(page, secondKey, 'Черновик второго')
  await page.getByRole('link', { name: 'Учёба', exact: true }).click()
  await page.waitForURL(url => url.searchParams.get('workspace') === workspace)
  await composer(page).fill('Проектный черновик')
  await waitSaved(page, projectKey, 'Проектный черновик')
  await page.reload()
  await waitText(page, 'Проектный черновик')

  await page.evaluate(key => {
    const version = JSON.parse(localStorage.getItem(key))
    version.quote = { messageId: 'example', text: 'Сохранённая цитата в черновике' }
    localStorage.setItem(key, JSON.stringify(version))
  }, firstKey)
  await page.goto(`/chats/${first}`)
  await waitText(page, 'Черновик первого')
  await page.getByText('Черновик восстановлен', { exact: true }).waitFor()
  await page.getByText('Прикрепите ранее выбранные файлы повторно.', { exact: true }).waitFor()
  await page.getByText('Сохранённая цитата в черновике', { exact: true }).waitFor()
  await page.getByRole('button', { name: 'Убрать цитату' }).click()

  const other = await context.newPage()
  await other.goto(`/chats/${first}`)
  await waitText(other, 'Черновик первого')
  await composer(other).fill('Версия соседней вкладки')
  await waitSaved(other, firstKey, 'Версия соседней вкладки')
  await page.getByText('В другой вкладке изменилась версия черновика.', { exact: true }).waitFor()
  assert.equal(await composer(page).inputValue(), 'Черновик первого')
  await composer(page).fill('Мой текст сохранён в поле')
  assert.equal(await composer(page).inputValue(), 'Мой текст сохранён в поле')
  await page.getByRole('button', { name: 'Загрузить версию из другой вкладки' }).click()
  await waitText(page, 'Версия соседней вкладки')
  await other.close()

  let tries = 0
  await page.route(`**/api/v1/chats/${first}/messages`, async route => {
    if (route.request().method() !== 'POST') return route.continue()
    if (!tries++) return route.fulfill({ status: 503, json: { title: 'Тест ошибки отправки' } })
    return route.continue()
  })
  const send = page.getByRole('button', { name: 'Отправить', exact: true })
  await send.click()
  await page.getByRole('alert').filter({ hasText: 'Тест ошибки отправки' }).waitFor()
  await page.reload()
  await waitText(page, 'Версия соседней вкладки')
  await send.click()
  await page.waitForFunction(key => localStorage.getItem(key) === null, firstKey)
  assert.equal(await page.evaluate(key => JSON.parse(localStorage.getItem(key)).text, secondKey), 'Черновик второго')
  assert.equal(await page.evaluate(key => JSON.parse(localStorage.getItem(key)).text, projectKey), 'Проектный черновик')
  await waitText(page, '')
  await page.goto(`/chats/${temporary}`)
  await page.getByText('Временный диалог', { exact: true }).waitFor()
  await composer(page).fill('Временный черновик')
  await page.evaluate(owner => localStorage.setItem(`budz.draft.${owner}.expired`,
    JSON.stringify({ text: 'Просрочено', updatedAt: Date.now() - 8 * 86400_000 })), owner)
  await page.reload()
  await waitText(page, '')
  assert.equal(await page.evaluate(key => localStorage.getItem(key), temporaryKey), null)

  await page.getByRole('link', { name: 'Второй чат', exact: true }).click()
  await waitText(page, 'Черновик второго')
  await page.waitForFunction(owner => !localStorage.getItem(`budz.draft.${owner}.expired`), owner)
  await page.getByRole('button', { name: 'Очистить черновик' }).click()
  await waitText(page, '')
  await page.getByRole('button', { name: 'Меню аккаунта' }).click()
  await page.getByRole('menuitem', { name: 'Выйти', exact: true }).click()
  await page.waitForURL('**/login')
  assert.equal(await page.evaluate(owner => Object.keys(localStorage).some(key => key.startsWith(`budz.draft.${owner}.`)), owner), false)
  await register()
  await page.goto('/chats')
  await waitText(page, '')
  console.log('Verified isolated drafts, expiry, tab conflicts, attachment reminder, retry, temporary mode and sign-out cleanup.')
} finally { await browser.close() }
