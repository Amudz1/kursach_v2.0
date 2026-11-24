import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const browser = await chromium.launch()
const context = await browser.newContext({
  baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080',
  viewport: { width: 320, height: 780 },
})
const page = await context.newPage()
const username = `files-${Date.now().toString(36)}`
const password = 'Budz-focus-files-2026'
const payload = (name, text = 'Текст для вложения') => ({ name, mimeType: 'text/plain', buffer: Buffer.from(text) })

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  assert.equal((await context.request.put('/api/v1/me/interface', { headers, data: { interfaceVariant: 'focus' } })).status(), 204)
  const temporary = await context.request.post('/api/v1/chats', { headers, data: { temporary: true } })
  assert.equal(temporary.status(), 201)
  const temporaryId = (await temporary.json()).id

  await page.goto('/chats')
  const focus = page.locator('.focus-chat')
  const picker = focus.getByLabel('Выбор файла')
  const files = focus.getByRole('list', { name: 'Приложенные файлы' })
  const alert = focus.getByRole('alert')
  const composer = focus.locator('.composer')
  const send = focus.getByRole('button', { name: 'Отправить', exact: true })
  const longName = `${'оченьдлинноеимя'.repeat(10)}.txt`
  await picker.setInputFiles([
    payload(longName), payload('application.exe'), payload('empty.txt', ''),
    { name: 'oversize.txt', mimeType: 'text/plain', buffer: Buffer.alloc(16 * 1024 * 1024 + 1) },
    { name: 'quota.txt', mimeType: 'text/plain', buffer: Buffer.alloc(3 * 1024 * 1024) },
  ])
  await files.getByText(longName, { exact: true }).waitFor()
  await alert.getByText(/application.exe.*Допустимые форматы/).waitFor()
  await alert.getByText(/empty.txt.*Файл пуст/).waitFor()
  await alert.getByText(/oversize.txt.*16 МБ/).waitFor()
  await alert.getByText(/quota.txt.*лимит тарифа/).waitFor()
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
  await files.getByRole('button', { name: `Удалить ${longName}` }).click()
  assert.ok(await send.isDisabled())

  const four = Array.from({ length: 5 }, (_, index) => payload(`file-${index + 1}.txt`))
  await picker.setInputFiles(four)
  await alert.getByText(/file-5.txt.*до четырёх/).waitFor()
  assert.equal(await files.getByRole('listitem').count(), 4)
  for (const { name } of four.slice(0, 4)) await files.getByRole('button', { name: `Удалить ${name}` }).click()

  await picker.setInputFiles([payload('good.txt'), payload('broken.pdf', 'Содержимое не является PDF')])
  await send.click()
  const rejected = files.getByRole('listitem').filter({ hasText: 'broken.pdf' })
  await rejected.getByRole('alert').filter({ hasText: /содержимое не соответствует расширению/ }).waitFor()
  assert.equal(await files.getByRole('listitem').filter({ hasText: 'good.txt' }).getByRole('alert').count(), 0)
  await rejected.getByRole('button', { name: 'Удалить broken.pdf' }).click()

  await composer.evaluate(form => {
    const transfer = new DataTransfer()
    transfer.items.add(new File(['Дополнительный текст'], 'dropped.md', { type: 'text/markdown' }))
    form.dispatchEvent(new DragEvent('dragenter', { dataTransfer: transfer, bubbles: true, cancelable: true }))
  })
  await composer.getByText('Отпустите, чтобы прикрепить').waitFor()
  await composer.evaluate(form => {
    const transfer = new DataTransfer()
    transfer.items.add(new File(['Дополнительный текст'], 'dropped.md', { type: 'text/markdown' }))
    form.dispatchEvent(new DragEvent('drop', { dataTransfer: transfer, bubbles: true, cancelable: true }))
  })
  await files.getByText('dropped.md', { exact: true }).waitFor()

  const requests = []
  await page.route(/\/api\/v1\/chats\/[^/]+\/messages(?:\?|$)/, async route => {
    if (route.request().method() !== 'POST') return route.continue()
    requests.push(route.request().postData()?.match(/name="requestId"\r?\n\r?\n([^\r\n]+)/)?.[1])
    if (requests.length === 1) return route.fulfill({ status: 503, json: { title: 'Проверка повтора с файлами' } })
    return route.continue()
  })
  await send.click()
  await alert.filter({ hasText: 'Проверка повтора с файлами' }).waitFor()
  assert.equal(await files.getByRole('listitem').count(), 2)
  await send.click()
  const first = focus.locator('.message.user').first()
  await first.getByText('Материалы во вложении.', { exact: true }).waitFor()
  await first.getByRole('button', { name: /good.txt/ }).waitFor()
  await first.getByRole('button', { name: /dropped.md/ }).waitFor()
  assert.ok(requests[0])
  assert.equal(requests[1], requests[0])

  const stop = focus.getByRole('button', { name: 'Остановить', exact: true })
  if (await stop.isVisible()) await stop.click()
  await send.waitFor()
  await picker.setInputFiles(payload('with-text.txt'))
  await focus.getByRole('textbox', { name: 'Сообщение для Budz' }).fill('Отправляю файл с текстом')
  await send.click()
  const second = focus.locator('.message.user').last()
  await second.getByText('Отправляю файл с текстом', { exact: true }).waitFor()
  await second.getByRole('button', { name: /with-text.txt/ }).waitFor()

  await page.goto(`/chats/${temporaryId}`)
  await focus.getByText('Временный диалог', { exact: true }).waitFor()
  assert.equal(await focus.getByRole('button', { name: 'Прикрепить' }).count(), 0)
  assert.equal(await picker.count(), 0)
  assert.ok(await send.isDisabled())
  console.log('Verified Focus files: count, format, size, server per-file errors, drag-and-drop, retries and temporary restrictions.')
} finally { await browser.close() }
