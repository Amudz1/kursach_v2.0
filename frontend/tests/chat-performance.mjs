import assert from 'node:assert/strict'
import { mkdir, writeFile } from 'node:fs/promises'
import { performance } from 'node:perf_hooks'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const benchmark = process.env.BUDZ_PERF ?? 'after'
const folder = process.env.BUDZ_CAPTURE_DIR
const browser = await chromium.launch({ args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] })
const setup = await browser.newContext({ baseURL })
const username = `perf-${Date.now().toString(36)}`
const password = 'Budz-performance-2026'
const round = Math.round
const median = values => values.sort((a, b) => a - b)[Math.floor(values.length / 2)]

try {
  const registered = await setup.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(registered.status(), 201)
  const grant = await registered.json()
  const headers = { Authorization: `Bearer ${grant.accessToken}` }
  const profile = await (await setup.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await setup.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  const entries = Array.from({ length: 80 }, (_, i) => ({
    sender: i % 2 ? 'assistant' : 'human', created_at: new Date(Date.UTC(2025, 1, 15, 12, i)).toISOString(),
    text: `## Запись ${i + 1}\n\n${'Тестовая длинная переписка Budz. '.repeat(12)}\n\n- Пункт проверки\n- Второй пункт`,
  }))
  const imported = await setup.request.post('/api/v1/data/import', { headers, multipart: {
    file: { name: 'performance.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify([{
      name: 'Длинная переписка', uuid: username, chat_messages: entries,
    }])) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const id = (await imported.json()).chatIds[0]
  const path = `/chats/${id}`

  const configure = variant => setup.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: variant } })
  const run = async variant => {
    const context = await browser.newContext({ baseURL, viewport: { width: 1440, height: 900 }, permissions: ['microphone'], reducedMotion: 'reduce' })
    await context.route('**/api/v1/auth/refresh', route => route.fulfill({ status: 200, json: grant }))
    const page = await context.newPage()
    const requests = []
    const sockets = []
    page.on('request', request => requests.push({ method: request.method(), path: new URL(request.url()).pathname }))
    page.on('websocket', websocket => sockets.push(new URL(websocket.url()).pathname))
    const composer = page.getByRole('textbox', { name: 'Сообщение для Budz' })
    const transcript = page.locator('.transcript')
    const start = performance.now()
    await page.goto(path, { waitUntil: 'commit' })
    await page.locator(`.workspace[data-interface="${variant}"]`).waitFor()
    await composer.waitFor()
    const ready = performance.now() - start
    const input = await composer.evaluate(element => new Promise(resolve => {
      requestAnimationFrame(() => {
        const started = performance.now()
        element.focus()
        element.value = 'Замер ввода'
        element.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: element.value }))
        requestAnimationFrame(() => resolve(performance.now() - started))
      })
    }))
    assert.equal(await composer.inputValue(), 'Замер ввода')
    await transcript.locator('article').first().waitFor()
    const initialJs = await page.evaluate(() => performance.getEntriesByType('resource')
      .filter(resource => new URL(resource.name).pathname.endsWith('.js')).reduce((total, resource) => total + resource.encodedBodySize, 0))
    const earlier = page.getByRole('button', { name: 'Загрузить ранние сообщения' })
    const previousStart = performance.now()
    await earlier.click()
    await page.waitForFunction(() => document.querySelectorAll('.transcript article').length >= 60)
    const earlierMs = performance.now() - previousStart
    const frame = await transcript.evaluate(element => new Promise(resolve => {
      let count = 0
      let previous = performance.now()
      const durations = []
      function step(now) {
        durations.push(now - previous)
        previous = now
        element.scrollTop += 44
        if (++count < 24) requestAnimationFrame(step)
        else resolve(durations.slice(1).sort((a, b) => a - b)[21])
      }
      requestAnimationFrame(step)
    }))
    const voiceBefore = requests.filter(item => /\/voice\/|voice-session|use-answer-speech/.test(item.path)).length
    if (benchmark === 'after') {
      assert.equal(voiceBefore, 0)
      if (variant === 'current') assert.equal(requests.some(item => item.path.includes('FocusChatPage')), false)
    }
    return { result: { readyMs: round(ready), firstInputMs: round(input), load30OlderMs: round(earlierMs),
      p95ScrollFrameMs: round(frame), initialJsBytes: initialJs, voiceRequestsBeforeAction: voiceBefore },
    context, page, requests, sockets }
  }
  const results = {}
  for (const variant of ['current', 'focus']) {
    assert.equal((await configure(variant)).status(), 204)
    const values = []
    for (let i = 0; i < 3; i++) {
      const sample = await run(variant)
      values.push(sample.result)
      await sample.context.close()
    }
    results[variant] = Object.fromEntries(Object.keys(values[0]).map(key => [key, median(values.map(item => item[key]))]))
  }
  assert.equal((await configure('current')).status(), 204)
  const sample = await run('current')
  const { page, requests, sockets } = sample
  await page.getByRole('textbox', { name: 'Сообщение для Budz' }).fill(`Расскажи: ${'а и '.repeat(26)}`)
  const afterSend = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname.endsWith('/messages'))
  await page.getByRole('button', { name: 'Отправить', exact: true }).click()
  await page.locator('.message.assistant .streaming').last().waitFor()
  await afterSend
  const at = {
    histories: requests.filter(item => item.method === 'GET' && item.path.endsWith('/messages')).length,
    connections: sockets.filter(item => item === '/hubs/chat').length,
    posts: requests.filter(item => item.method === 'POST' && item.path.endsWith('/messages')).length,
  }
  await page.getByRole('button', { name: 'Меню аккаунта' }).click()
  const start = performance.now()
  await page.getByRole('menuitemradio', { name: 'Фокус', exact: true }).click()
  await page.locator('.focus-chat').waitFor()
  assert.ok(await page.getByRole('button', { name: 'Остановить', exact: true }).isVisible())
  results.switchMs = round(performance.now() - start)
  results.switchNetwork = { histories: requests.filter(item => item.method === 'GET' && item.path.endsWith('/messages')).length - at.histories,
    connections: sockets.filter(item => item === '/hubs/chat').length - at.connections,
    posts: requests.filter(item => item.method === 'POST' && item.path.endsWith('/messages')).length - at.posts,
    activeConnections: at.connections }
  assert.deepEqual(results.switchNetwork, { histories: 0, connections: 0, posts: 0, activeConnections: 1 })
  await page.getByRole('button', { name: 'Остановить', exact: true }).click()
  if (benchmark === 'after') {
    await page.getByRole('button', { name: 'Записать голос' }).click()
    await page.getByRole('button', { name: 'Отменить запись' }).waitFor()
    assert.equal(requests.filter(item => item.path.endsWith('/voice/capabilities')).length, 1)
    assert.ok(requests.some(item => item.path.includes('voice-session')))
    await page.getByRole('button', { name: 'Отменить запись' }).click()
    const speechChunk = page.waitForRequest(request => new URL(request.url()).pathname.includes('use-answer-speech'))
    await page.locator('.message.assistant').first().getByRole('button', { name: 'Озвучить', exact: true }).click()
    await speechChunk
  }
  await sample.context.close()
  console.log(JSON.stringify(results, null, 2))
  if (folder) {
    await mkdir(folder, { recursive: true })
    await writeFile(`${folder}/${benchmark}.json`, JSON.stringify(results, null, 2))
  }
} finally { await browser.close() }
