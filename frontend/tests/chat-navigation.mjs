import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { join } from 'node:path'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const captureDir = process.env.BUDZ_CAPTURE_DIR
const browser = await chromium.launch()
const context = await browser.newContext({ baseURL, viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' })
const page = await context.newPage()
const username = `reading-${Date.now().toString(36)}`
const password = 'Budz-reading-demo-2026'
const checks = []
page.on('pageerror', error => checks.push(error.message))
const button = name => page.getByRole('button', { name, exact: true })
const heading = name => page.getByRole('heading', { name, exact: true })

async function capture(name) {
  if (captureDir) await page.screenshot({ path: join(captureDir, name + '.png'), animations: 'disabled' })
}
async function openSearch() {
  await page.locator('.chat-menu > summary').click()
  await button('Поиск по беседе').click()
}
async function noOverflow(target = page) {
  assert.ok(await target.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
}

try {
  if (captureDir) await mkdir(captureDir, { recursive: true })
  const signup = await context.request.post('/api/v1/auth/register', {
    data: { username, email: username + '@demo.invalid', password, passwordConfirmation: password },
  })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: 'Bearer ' + (await signup.json()).accessToken }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', {
    headers, data: { ...profile, onboardingCompleted: true },
  })).ok())

  await page.goto('/chats')
  await heading('Что хотите разобрать?').waitFor()
  assert.equal(await page.getByRole('link', { name: /Продолжить/ }).count(), 0)

  const messages = Array.from({ length: 82 }, (_, index) => ({
    role: index % 2 ? 'assistant' : 'user',
    content: index === 1 ? '## Важная подсказка\n\n```cs\nvar lighthouse = 7;\n```' :
      index === 3 ? 'Здесь повторяется lighthouse.' :
      `Реплика ${index}\n\n` + 'Это пример сообщения в длинной истории для проверки навигации. '.repeat(8),
    createdAt: new Date(Date.UTC(2025, 0, 1, 0, index)).toISOString(),
  }))
  const loaded = await context.request.post('/api/v1/data/import', { headers, multipart: {
    file: { name: 'reading.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({
      conversations: [{ title: 'Экспедиция', messages }],
    })) },
  } })
  assert.ok(loaded.ok(), await loaded.text())
  const { chatIds: [chatId] } = await loaded.json()
  const chat = `/chats/${chatId}`
  await page.goto(chat)
  await heading('Экспедиция').waitFor()
  await page.getByText('Реплика 81', { exact: true }).waitFor()
  assert.equal(await heading('Важная подсказка').count(), 0)

  await openSearch()
  const search = page.getByRole('dialog', { name: 'Поиск по беседе' })
  await search.getByRole('searchbox').fill('unknown phrase')
  await search.getByText('Совпадений нет.').waitFor()
  await search.getByRole('searchbox').fill('LIGHTHOUSE')
  await search.locator('mark').last().waitFor()
  assert.equal(await search.locator('mark').count(), 2)
  await capture('search-desktop')
  await search.getByRole('button', { name: 'Следующее' }).click()
  await heading('Важная подсказка').waitFor()
  const answer = page.locator('article.message').filter({ has: heading('Важная подсказка') })
  const messageId = await answer.getAttribute('data-reading-message')
  assert.equal(await answer.getAttribute('id'), 'message-' + messageId)
  assert.equal(await answer.evaluate(element => element === document.activeElement), true)
  await answer.getByRole('button', { name: 'Добавить закладку' }).click()
  await answer.getByRole('button', { name: 'Убрать закладку', pressed: true }).waitFor()
  await capture('wave-desktop')
  await openSearch()
  await search.getByRole('button', { name: 'Следующее' }).click()
  await page.locator('.transcript').getByText('Здесь повторяется lighthouse.', { exact: true }).waitFor()
  await openSearch()
  await search.getByRole('button', { name: 'Предыдущее' }).click()
  await heading('Важная подсказка').waitFor()

  await page.setViewportSize({ width: 320, height: 820 })
  await noOverflow()
  await button('Оглавление').click()
  const outline = page.getByRole('dialog', { name: 'Оглавление' })
  await outline.getByRole('heading', { name: 'Закладки' }).waitFor()
  const saved = outline.getByRole('button', { name: /Важная подсказка/ })
  await saved.waitFor()
  const panel = await outline.boundingBox()
  assert.ok(panel.y + panel.height >= 819 && panel.width <= 320)
  await capture('outline-mobile')
  await page.keyboard.press('Escape')
  await outline.waitFor({ state: 'hidden' })
  assert.equal(await button('Оглавление').evaluate(element => element === document.activeElement), true)
  await button('Оглавление').click()
  await saved.click()
  await heading('Важная подсказка').waitFor()
  await page.waitForFunction(id => {
    const account = Object.keys(localStorage).find(key => key.startsWith('budz.reading.'))
    return account && JSON.parse(localStorage.getItem(account))[0]?.messageId === id
  }, messageId)
  await page.goto('/chats')
  await heading('Что хотите разобрать?').waitFor()
  const resume = page.getByRole('link', { name: /Продолжить.*Экспедиция/ })
  await resume.waitFor()
  assert.match(await resume.getAttribute('href'), new RegExp(messageId))
  await capture('resume-mobile')
  await resume.click()
  await heading('Важная подсказка').waitFor()

  const other = await browser.newContext({ baseURL, reducedMotion: 'reduce', colorScheme: 'dark' })
  try {
    assert.ok((await other.request.post('/api/v1/auth/login', { data: { login: username, password } })).ok())
    const next = await other.newPage()
    await next.goto(chat)
    await next.getByRole('button', { name: 'Оглавление', exact: true }).click()
    await next.getByRole('dialog').getByRole('button', { name: /Важная подсказка/ }).click()
    await next.getByRole('heading', { name: 'Важная подсказка' }).waitFor()
    if (captureDir) await next.screenshot({ path: join(captureDir, 'wave-dark-desktop.png'), animations: 'disabled' })
    await next.getByRole('button', { name: 'Убрать закладку' }).click()
    await next.locator(`#message-${messageId}`).getByRole('button', { name: 'Добавить закладку' }).waitFor()
    await noOverflow(next)
  } finally { await other.close() }

  await page.goto('/appearance')
  await heading('Оформление').waitFor()
  const setting = page.getByLabel('Обводка сообщений', { exact: true })
  await setting.selectOption('none')
  const sample = page.locator('.preview-message.assistant')
  assert.equal(await sample.evaluate(element => getComputedStyle(element, '::after').content), 'none')
  await setting.selectOption('side')
  await button('Сохранить').click()
  await page.getByText('Оформление сохранено.', { exact: true }).waitFor()
  await page.reload()
  await heading('Оформление').waitFor()
  assert.equal(await setting.inputValue(), 'side')
  await noOverflow()
  assert.deepEqual(checks, [])
  console.log('Verified search and jumps in 82 messages, bookmarks across browser contexts, mobile panels, resume position and new outlines.')
} catch (error) {
  await capture('failed')
  throw error
} finally { await browser.close() }
