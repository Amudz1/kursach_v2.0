import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const target = process.env.BUDZ_CAPTURE_DIR ?? fileURLToPath(new URL('../../.local/focus-acceptance/', import.meta.url))
const desktop = { width: 1440, height: 900 }
const phone = { width: 390, height: 844 }
const title = 'План подготовки к экзамену'
const question = 'До экзамена неделя. Как распределить повторение и практику?'
const answer = [
  '## Подготовка на семь дней',
  'Чередуйте повторение с практикой. Так будет видно, какие темы требуют ещё одного подхода.',
  '| Дни | Что сделать |\n| --- | --- |\n| 1–2 | Выберите две трудные темы. |\n| 3–5 | Решите короткие серии задач. |\n| 6–7 | Разберите ошибки и повторите ключевое. |',
].join('\n\n')
const browser = await chromium.launch({ args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] })
const setup = await browser.newContext({ baseURL })
const username = `demo-focus-${Date.now().toString(36)}`
const password = 'Budz-acceptance-demo-2026'

try {
  await mkdir(target, { recursive: true })
  const signup = await setup.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const grant = await signup.json()
  const headers = { Authorization: `Bearer ${grant.accessToken}` }
  const profile = await (await setup.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await setup.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())

  const project = await setup.request.post('/api/v1/workspaces', { headers, data: {
    name: 'Учёба', instructions: 'Конспекты и подготовка к экзаменам', materials: '',
  } })
  assert.equal(project.status(), 201)
  const workspaceId = (await project.json()).id
  const otherWorkspace = await setup.request.post('/api/v1/workspaces', { headers, data: {
    name: 'Рабочие заметки', instructions: '', materials: '',
  } })
  assert.equal(otherWorkspace.status(), 201)

  async function importChat(name, messages, workspaceId) {
    const archive = JSON.stringify([{ name, uuid: `${username}-${name}`,
      chat_messages: messages.map(([sender, text], index) => ({
        sender, text, created_at: new Date(Date.UTC(2026, 10, 20, 9, 14 + index)).toISOString(),
      })) }])
    const multipart = { file: { name: 'demo-history.json', mimeType: 'application/json', buffer: Buffer.from(archive) } }
    if (workspaceId) multipart.workspaceId = workspaceId
    const response = await setup.request.post('/api/v1/data/import', { headers, multipart })
    assert.ok(response.ok(), await response.text())
    return (await response.json()).chatIds[0]
  }
  const chatId = await importChat(title, [['human', question], ['assistant', answer]], workspaceId)
  const messageList = await (await setup.request.get(`/api/v1/chats/${chatId}/messages`, { headers })).json()
  const responseId = messageList.items.find(message => message.role === 'assistant').id
  assert.equal((await setup.request.patch(`/api/v1/chats/${chatId}`, { headers, data: { isPinned: true } })).status(), 204)
  assert.equal((await setup.request.put(`/api/v1/chats/${chatId}/bookmarks/${responseId}`, { headers })).status(), 204)
  await importChat('Конспект по алгоритмам', [['human', 'Где использовать бинарный поиск?']], workspaceId)
  await importChat('Идеи для выступления', [['human', 'Как вступить в тему?']])
  const chatUrl = `/chats/${chatId}?workspace=${workspaceId}`
  const appearance = await (await setup.request.get('/api/v1/me/appearance', { headers })).json()

  for (const theme of ['light', 'dark']) {
    assert.ok((await setup.request.put('/api/v1/me/appearance', { headers, data: {
      schemaVersion: appearance.schemaVersion, preset: 'studio', colorMode: theme, settings: { motion: 'reduced' },
    } })).ok())
    for (const variant of ['current', 'focus']) {
      assert.equal((await setup.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: variant } })).status(), 204)
      const context = await browser.newContext({ baseURL, viewport: desktop, permissions: ['microphone'],
        reducedMotion: 'reduce', locale: 'ru-RU', timezoneId: 'Europe/Moscow' })
      try {
        await context.route('**/api/v1/auth/refresh', route => route.fulfill({ status: 200, json: grant }))
        const page = await context.newPage()
        const layout = page.locator(`.workspace[data-interface="${variant}"]`)
        const composer = layout.getByRole('textbox', { name: 'Сообщение для Budz' })
        const snap = async name => {
          await page.evaluate(async () => { await document.fonts.ready })
          await page.waitForFunction(mode => document.documentElement.dataset.theme === mode, theme)
          assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
          await page.screenshot({ path: join(target, `${name}-${theme}.png`), animations: 'disabled', caret: 'hide' })
        }

        await page.goto(chatUrl)
        await layout.locator('.message.assistant').getByRole('heading', { name: 'Подготовка на семь дней' }).waitFor()
        await composer.fill('Как разбить занятия на короткие блоки?')
        await layout.getByLabel('Выбор файла').setInputFiles({ name: 'план-на-неделю.md', mimeType: 'text/markdown',
          buffer: Buffer.from('# План на неделю\n\n- Повторение\n- Практика\n- Разбор ошибок\n') })
        await layout.getByText('план-на-неделю.md', { exact: true }).waitFor()
        await layout.locator('.transcript').evaluate(el => { el.scrollTop = 0 })
        await snap(`${variant}-chat-desktop`)
        await page.setViewportSize(phone)
        await layout.locator('.transcript').evaluate(el => { el.scrollTop = 0 })
        await snap(`${variant}-chat-mobile`)
        if (variant !== 'focus') continue

        await page.getByRole('button', { name: 'Показать список диалогов' }).click()
        await page.getByRole('dialog', { name: 'История диалогов', exact: true }).waitFor()
        await snap('focus-history-mobile')
        await page.keyboard.press('Escape')
        await layout.getByRole('button', { name: 'Очистить черновик' }).click()
        await composer.fill('Помоги детализировать расписание.')
        await layout.getByRole('button', { name: 'Записать голос', exact: true }).click()
        const panel = layout.getByRole('group', { name: 'Запись голоса' })
        await panel.getByText('Идёт запись', { exact: true }).waitFor()
        await page.waitForFunction(() => document.querySelector('.voice-time')?.textContent === '0:02')
        await snap('focus-recording-mobile')
        await panel.getByRole('button', { name: 'Завершить запись' }).click()
        await panel.getByRole('button', { name: 'Прослушать' }).waitFor()
        await snap('focus-preview-mobile')
        await panel.getByRole('button', { name: 'Расшифровать', exact: true }).click()
        await panel.waitFor({ state: 'hidden' })
        assert.equal(await composer.inputValue(), 'Помоги детализировать расписание. Это тестовая расшифровка Budz.')
        await composer.fill('Составь расписание повторения на неделю.')
        await snap('focus-transcript-mobile')
        await layout.getByRole('button', { name: 'Очистить черновик' }).click()
        await page.setViewportSize(desktop)
        await page.goto('/chats')
        await layout.getByRole('heading', { name: 'С чего начнём?' }).waitFor()
        await layout.locator('.resume-chat').waitFor()
        await snap('focus-start-desktop')
        await page.goto('/appearance')
        await page.getByRole('heading', { name: 'Интерфейс', exact: true }).waitFor()
        await snap('focus-picker-desktop')
      } finally { await context.close() }
    }
  }
  console.log('Captured 20 screenshots from the same synthetic conversation and voice flow in both color modes.')
} finally { await browser.close() }
