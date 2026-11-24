import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const baseURL = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch()
const context = await browser.newContext({ baseURL })
const page = await context.newPage()
const username = `layout-${Date.now().toString(36)}`
const password = 'Budz-interface-test-2026'
const focusChunk = /\/assets\/FocusChatPage-[^/]+\.js(?:\?|$)/
const loaded = []
page.on('request', request => { if (focusChunk.test(request.url())) loaded.push(request.url()) })

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  const owner = await (await context.request.get('/api/v1/me', { headers })).json()
  await page.addInitScript(({ id }) => {
    localStorage.setItem(`budz.interface.${id}`, 'focus')
  }, owner)
  await page.goto('/chats')
  await page.locator('[data-interface="current"]').waitFor()
  assert.equal(loaded.length, 0)
  assert.equal(await page.evaluate(id => localStorage.getItem(`budz.interface.${id}`), owner.id), 'current')

  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)
  let failedUrl
  await page.route(focusChunk, async route => {
    failedUrl ??= route.request().url()
    if (route.request().url() === failedUrl) await route.abort('failed')
    else await route.continue()
  })
  await page.reload()
  await page.getByText('Интерфейс не загрузился.', { exact: true }).waitFor()
  await page.getByRole('button', { name: 'Повторить', exact: true }).click()
  await page.locator('[data-interface="focus"]').waitFor()
  assert.equal(new Set(loaded).size, 2)

  const other = await browser.newContext({ baseURL })
  try {
    const login = await other.request.post('/api/v1/auth/login', { data: { login: username, password } })
    assert.equal(login.status(), 200)
    const next = await other.newPage()
    await next.route(focusChunk, route => route.abort('failed'))
    await next.goto('/chats')
    await next.getByRole('button', { name: 'Открыть текущий Budz' }).click()
    await next.locator('[data-interface="current"]').waitFor()
    assert.equal(await next.getByText('Интерфейс не загрузился.', { exact: true }).count(), 0)
    await next.unroute(focusChunk)
    await next.reload()
    await next.locator('[data-interface="focus"]').waitFor()
  } finally { await other.close() }

  console.log('Verified default lazy loading, server selection, chunk retry and temporary fallback on a fresh device.')
} finally { await browser.close() }
