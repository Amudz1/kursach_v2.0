import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch()
const context = await browser.newContext({ baseURL, viewport: { width: 1440, height: 850 } })
await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: baseURL })
const page = await context.newPage()
const username = `reading-${Date.now().toString(36)}`
const password = 'Budz-focus-transcript-2026'
const captureDir = process.env.BUDZ_CAPTURE_DIR

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)
  const project = await context.request.post('/api/v1/workspaces', { headers, data: { name: 'Разработка', instructions: '', materials: '' } })
  assert.equal(project.status(), 201)
  const workspaceId = (await project.json()).id

  const initial = await (await context.request.get('/api/v1/me/appearance', { headers })).json()
  const saveAppearance = options => context.request.put('/api/v1/me/appearance', { headers, data: {
    schemaVersion: initial.schemaVersion, preset: 'studio', colorMode: 'light', settings: {
      headingColor: '#225533', messageActions: 'always', ...options,
    },
  } })
  assert.ok((await saveAppearance({})).ok())
  assert.equal((await saveAppearance({ headingColor: 'red' })).status(), 400)

  const row = Array.from({ length: 5 }, (_, i) => `Колонка ${i + 1} с длинным заголовком`)
  const table = [row, row.map(() => '---'), row.map(() => 'Значениепараметра'.repeat(5))]
    .map(values => `| ${values.join(' | ')} |`).join('\n')
  const code = `const оченьДлинноеИмяПеременной = "${'продолжение'.repeat(22)}";\nconsole.log(оченьДлинноеИмяПеременной);`
  const rich = ['## Заголовок ответа', '**Выделение** и обычный текст.', '- Первый пункт\n- Второй пункт',
    table, ['```javascript', code, '```'].join('\n')].join('\n\n')
  const messages = Array.from({ length: 42 }, (_, index) => ({
    sender: index % 2 ? 'assistant' : 'human',
    text: index === 41 ? rich : index === 40 ? '## Заголовок пользователя' : `Сообщение №${index + 1}\n\n${'Параграф переписки. '.repeat(9)}`,
    created_at: new Date(Date.UTC(2025, 1, 15, 12, index)).toISOString(),
  }))
  const imported = await context.request.post('/api/v1/data/import', { headers, multipart: {
    workspaceId, file: { name: 'history.json', mimeType: 'application/json',
      buffer: Buffer.from(JSON.stringify([{ name: 'Старая переписка', uuid: username, chat_messages: messages }])) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const chatId = (await imported.json()).chatIds[0]

  await page.goto(`/chats/${chatId}?workspace=${workspaceId}`)
  const focus = page.locator('.focus-chat')
  const transcript = focus.getByRole('region', { name: 'Сообщения', exact: true })
  const top = focus.locator('.conversation-header')
  await top.getByRole('heading', { name: 'Старая переписка', exact: true }).waitFor()
  await top.locator('.chat-scope-name').filter({ hasText: /^Разработка$/ }).waitFor()
  await top.getByRole('button', { name: 'Оглавление' }).waitFor()
  await top.getByLabel('Действия диалога').waitFor()
  const heading = transcript.getByRole('heading', { name: 'Заголовок ответа' })
  await heading.waitFor()
  assert.equal(await heading.evaluate(el => getComputedStyle(el).color), 'rgb(34, 85, 51)')
  assert.notEqual(await transcript.getByRole('heading', { name: 'Заголовок пользователя' }).evaluate(el => getComputedStyle(el).color), 'rgb(34, 85, 51)')
  const answer = transcript.locator('.message.assistant').last()
  const userMessage = transcript.locator('.message.user').last()
  await answer.getByRole('button', { name: 'Номера строк' }).click()
  assert.equal((await answer.locator('.code-gutter').textContent()).trim(), '1\n2')
  await answer.getByRole('button', { name: 'Копировать код' }).click()
  await page.waitForFunction(async value => (await navigator.clipboard.readText()).trim() === value, code)
  await answer.getByRole('button', { name: 'Копировать', exact: true }).click()
  await page.waitForFunction(async value => await navigator.clipboard.readText() === value, rich)
  await userMessage.getByRole('button', { name: 'Копировать сообщение' }).click()
  await page.waitForFunction(async () => await navigator.clipboard.readText() === '## Заголовок пользователя')
  assert.ok(await userMessage.locator('.message-copy').evaluate(el => el.getBoundingClientRect().right <= el.closest('article').getBoundingClientRect().left + 1))
  if (captureDir) {
    await mkdir(captureDir, { recursive: true })
    await page.screenshot({ path: `${captureDir}/focus-transcript.png` })
  }

  const earlier = transcript.getByRole('button', { name: 'Загрузить ранние сообщения' })
  let releaseOlder
  let requestOlder
  const olderRequested = new Promise(resolve => { requestOlder = resolve })
  const olderReady = new Promise(resolve => { releaseOlder = resolve })
  await page.route(url => url.pathname.endsWith('/messages') && url.searchParams.has('before'), async route => {
    requestOlder()
    await olderReady
    await route.continue()
  })
  await earlier.click()
  await olderRequested
  const readingAnchor = await transcript.evaluate(el => {
    el.scrollTop = 270
    const line = el.getBoundingClientRect().top + 8
    const anchor = [...el.querySelectorAll('[data-reading-message]')].find(message => message.getBoundingClientRect().bottom > line)
    return { id: anchor.id, top: anchor.getBoundingClientRect().top }
  })
  releaseOlder()
  await transcript.getByText('Сообщение №1', { exact: true }).waitFor({ state: 'attached' })
  await page.waitForFunction(({ id, top }) => Math.abs(document.getElementById(id).getBoundingClientRect().top - top) <= 2, readingAnchor)

  const field = focus.getByRole('textbox', { name: 'Сообщение для Budz' })
  await field.fill('Объясни алгоритм и покажи пример кода')
  await focus.getByRole('button', { name: 'Отправить', exact: true }).click()
  const stop = focus.getByRole('button', { name: 'Остановить', exact: true })
  await stop.waitFor()
  const nextAnswer = transcript.locator('.message.assistant').last()
  await nextAnswer.locator('.streaming').waitFor()
  await transcript.evaluate(el => { el.scrollTop = 440 })
  await nextAnswer.locator('.streaming').waitFor({ state: 'hidden' })
  assert.ok(await transcript.evaluate(el => Math.abs(el.scrollTop - 440) <= 2))
  await focus.getByRole('button', { name: 'К последнему сообщению' }).click()
  await page.waitForFunction(() => {
    const list = document.querySelector('.focus-chat .transcript')
    return list.scrollHeight - list.scrollTop - list.clientHeight <= 2
  })

  await page.setViewportSize({ width: 320, height: 780 })
  const archivedAnswer = transcript.getByRole('heading', { name: 'Заголовок ответа' }).locator('xpath=ancestor::article')
  const ownCopy = transcript.getByRole('heading', { name: 'Заголовок пользователя' }).locator('xpath=ancestor::article').locator('.message-copy')
  await ownCopy.scrollIntoViewIfNeeded()
  assert.ok(await ownCopy.evaluate(el => {
    const control = el.getBoundingClientRect()
    const frame = el.closest('article').getBoundingClientRect()
    return control.left >= frame.left && Math.abs(control.left - frame.left) < 16 && control.width >= 44
  }))
  const more = nextAnswer.getByRole('button', { name: 'Ещё', exact: true })
  await more.click()
  assert.equal(await more.getAttribute('aria-expanded'), 'true')
  await nextAnswer.getByRole('button', { name: 'Полезно' }).click()
  await page.waitForFunction(() => document.querySelector('.message.assistant:last-of-type button[aria-label="Полезно"]')?.getAttribute('aria-pressed') === 'true')
  assert.ok(await archivedAnswer.locator('table').evaluate(el => el.scrollWidth > el.clientWidth))
  assert.ok(await archivedAnswer.locator('pre:not(.code-gutter)').evaluate(el => el.scrollWidth > el.clientWidth))
  const outlines = ['rectangle', 'wave', 'rounded', 'oval', 'none', 'side']
  const styles = new Set()
  for (const scheme of ['light', 'dark']) for (const outline of outlines) {
    await page.evaluate(({ scheme, outline }) => Object.assign(document.documentElement.dataset, { theme: scheme, messageOutline: outline }), { scheme, outline })
    const border = await archivedAnswer.evaluate(el => {
      const stroke = getComputedStyle(el, '::after')
      return [getComputedStyle(el).borderRadius, stroke.maskImage, stroke.content, stroke.boxShadow === 'none'].join('|')
    })
    styles.add(border)
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
  }
  assert.equal(styles.size, 6)
  console.log('Verified Focus transcript: archived Markdown, colors, copy, table, code, stable reading and compact actions.')
} finally { await browser.close() }
