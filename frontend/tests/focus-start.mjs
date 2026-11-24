import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const context = await browser.newContext({
  baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080',
  viewport: { width: 1440, height: 900 },
})
const page = await context.newPage()
const username = `start-${Date.now().toString(36)}`
const password = 'Budz-focus-start-2026'
const captureDir = process.env.BUDZ_CAPTURE_DIR

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const account = await signup.json()
  const headers = { Authorization: `Bearer ${account.accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)
  const owner = await (await context.request.get('/api/v1/me', { headers })).json()
  const project = await context.request.post('/api/v1/workspaces', { headers, data: {
    name: 'Учёба', instructions: 'Подготовка к экзаменам', materials: '',
  } })
  assert.equal(project.status(), 201)
  const workspaceId = (await project.json()).id

  const creations = []
  const submissions = []
  page.on('request', request => {
    if (request.method() !== 'POST') return
    const path = new URL(request.url()).pathname
    if (path === '/api/v1/chats') creations.push(request.postDataJSON())
    if (/\/chats\/[^/]+\/messages$/.test(path)) submissions.push(path)
  })

  await page.goto('/chats')
  const focus = page.locator('.focus-chat')
  await focus.getByRole('heading', { level: 1, name: 'С чего начнём?' }).waitFor()
  const field = focus.getByRole('textbox', { name: 'Сообщение для Budz' })
  assert.equal(await field.getAttribute('placeholder'), 'Напишите вопрос или запишите голос…')
  assert.notEqual(await field.evaluate(el => document.activeElement === el), true)
  const suggestions = focus.locator('.focus-start-ideas')
  assert.equal(await suggestions.getByRole('button').count(), 4)
  if (captureDir) {
    await mkdir(captureDir, { recursive: true })
    await page.screenshot({ path: `${captureDir}/focus-start.png` })
  }
  await field.fill('Разбираю ваш ответ.')
  await suggestions.getByRole('button', { name: 'Объяснить тему' }).click()
  assert.equal(await field.inputValue(), 'Разбираю ваш ответ.\n\nОбъясни простыми словами тему: ')
  assert.ok(await field.evaluate(el => document.activeElement === el && el.selectionStart === el.value.length))
  assert.equal(creations.length, 0)
  assert.equal(submissions.length, 0)
  const send = focus.getByRole('button', { name: 'Отправить', exact: true })
  await send.click()
  await page.waitForURL(/\/chats\/[a-z\d-]+/)
  await focus.locator('.message.user').first().waitFor()
  assert.deepEqual(creations, [{ workspaceId: null }])
  const personalId = new URL(page.url()).pathname.split('/').at(-1)
  const personal = await (await context.request.get(`/api/v1/chats/${personalId}`, { headers })).json()
  assert.equal(personal.workspaceId ?? null, null)

  const projectLink = focus.getByRole('navigation', { name: 'Список проектов' }).getByRole('link', { name: 'Учёба' })
  await projectLink.click()
  await page.waitForURL(url => url.pathname === '/chats' && url.searchParams.get('workspace') === workspaceId)
  await focus.getByRole('heading', { level: 1, name: 'Учёба' }).waitFor()
  const projectThreads = focus.getByRole('button', { name: 'Диалоги проекта' })
  assert.equal(await projectThreads.getAttribute('aria-expanded'), 'false')
  await projectThreads.click()
  await focus.getByText('В проекте пока нет диалогов.').waitFor()
  await projectThreads.click()
  await focus.getByRole('button', { name: 'Настройки проекта' }).click()
  const editor = page.getByRole('dialog')
  await editor.getByText('Настройки проекта', { exact: true }).waitFor()
  await page.keyboard.press('Escape')
  await editor.waitFor({ state: 'hidden' })
  if (captureDir) await page.screenshot({ path: `${captureDir}/focus-project.png` })

  await page.setViewportSize({ width: 320, height: 850 })
  assert.ok((await field.boundingBox()).y < 540)
  assert.notEqual(await field.evaluate(el => document.activeElement === el), true)
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
  await field.fill('План подготовки к экзаменам')
  await send.click()
  await page.waitForURL(url => url.pathname.startsWith('/chats/') && url.searchParams.get('workspace') === workspaceId)
  const projectChatId = new URL(page.url()).pathname.split('/').at(-1)
  const userMessage = focus.locator('.message.user').first()
  await userMessage.getByText('План подготовки к экзаменам', { exact: true }).waitFor()
  assert.deepEqual(creations, [{ workspaceId: null }, { workspaceId }])
  assert.equal(submissions.length, 2)
  const projectChat = await (await context.request.get(`/api/v1/chats/${projectChatId}`, { headers })).json()
  assert.equal(projectChat.workspaceId, workspaceId)

  const messageId = await userMessage.getAttribute('data-reading-message')
  assert.ok(messageId)
  const savedCard = await context.request.get(`/api/v1/chats/resume?workspaceId=${workspaceId}&chatId=${projectChatId}&messageId=${messageId}`, { headers })
  assert.equal(savedCard.status(), 200)
  assert.equal((await savedCard.json()).anchorId, messageId)
  await page.goto(`/chats?workspace=${workspaceId}`)
  await focus.getByRole('heading', { level: 1, name: 'Учёба' }).waitFor()
  await page.evaluate(({ ownerId, chatId, workspaceId, messageId }) => {
    localStorage.setItem(`budz.reading.${ownerId}`, JSON.stringify([{ chatId, workspaceId, messageId, offset: 12, time: Date.now() }]))
  }, { ownerId: owner.id, chatId: projectChatId, workspaceId, messageId })
  await page.reload()
  const resume = focus.locator('a.resume-chat')
  await resume.waitFor()
  const resumeUrl = new URL(await resume.getAttribute('href'), page.url())
  assert.equal(resumeUrl.pathname, `/chats/${projectChatId}`)
  assert.equal(resumeUrl.searchParams.get('workspace'), workspaceId)
  assert.equal(resumeUrl.searchParams.get('at'), messageId)
  assert.equal(resumeUrl.searchParams.get('offset'), '12')
  assert.match(await resume.innerText(), /План подготовки к экзаменам/)
  assert.match(await resume.innerText(), /Учёба/)
  console.log('Verified Focus start: editable suggestions, personal and project chat creation, project controls and saved position.')
} finally { await browser.close() }
