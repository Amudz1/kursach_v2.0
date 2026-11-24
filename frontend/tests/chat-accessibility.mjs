import assert from 'node:assert/strict'
import { chromium } from 'playwright'

const browser = await chromium.launch({ args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] })
const context = await browser.newContext({
  baseURL: process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080', viewport: { width: 1440, height: 900 },
  reducedMotion: 'reduce', permissions: ['microphone'], hasTouch: true,
})
const page = await context.newPage()
const username = `access-${Date.now().toString(36)}`
const password = 'Budz-accessibility-2026'
const bodyText = 'Проверка обычного текста в беседе.'
const fixture = ['## Проверка заголовка', bodyText, ['```javascript', `const word = "${'длиннаястрока'.repeat(45)}";`, '```'].join('\n'),
  `| Колонка 1 | Колонка 2 |\n|---|---|\n| ${'неразрывныйтекст'.repeat(20)} | значимое поле |`].join('\n\n')

try {
  const signup = await context.request.post('/api/v1/auth/register', { data: {
    username, email: `${username}@demo.invalid`, password, passwordConfirmation: password,
  } })
  assert.equal(signup.status(), 201)
  const headers = { Authorization: `Bearer ${(await signup.json()).accessToken}` }
  const profile = await (await context.request.get('/api/v1/me/personalization', { headers })).json()
  assert.ok((await context.request.put('/api/v1/me/personalization', { headers, data: { ...profile, onboardingCompleted: true } })).ok())
  const imported = await context.request.post('/api/v1/data/import', { headers, multipart: {
    file: { name: 'access.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify([{
      name: 'Отдельная прокрутка', uuid: username, chat_messages: [
        { sender: 'human', text: 'Проверь широкие блоки.' }, { sender: 'assistant', text: fixture },
      ],
    }])) },
  } })
  assert.ok(imported.ok(), await imported.text())
  const chat = (await imported.json()).chatIds[0]
  await page.goto(`/chats/${chat}`)
  const composer = page.getByRole('textbox', { name: 'Сообщение для Budz' })
  const accountButton = page.getByRole('button', { name: 'Меню аккаунта', exact: true })
  const accessButton = page.getByRole('button', { name: 'Показать список диалогов' })
  const answer = page.locator('.message.assistant')
  const history = page.getByRole('dialog', { name: 'История диалогов', exact: true })
  const send = page.getByRole('button', { name: 'Отправить', exact: true })
  async function minTouch(button) {
    const box = await button.boundingBox()
    assert.ok(box && box.width >= 43.95 && box.height >= 43.95, JSON.stringify(box))
  }
  async function noOverflow(label) {
    const actual = await page.evaluate(() => ({
      page: document.documentElement.scrollWidth,
      viewport: document.documentElement.clientWidth,
      transcript: document.querySelector('.transcript').scrollWidth,
      content: document.querySelector('.transcript').clientWidth,
    }))
    assert.ok(actual.page <= actual.viewport + 1 && actual.transcript <= actual.content + 1, `${label} ${JSON.stringify(actual)}`)
  }
  async function setAppearance(preset, theme, scale) {
    await page.evaluate(({ preset, theme, scale }) => Object.assign(document.documentElement.dataset, {
      preset, theme, fontScale: scale, motion: 'reduced',
    }), { preset, theme, scale })
  }
  async function contrast(selector) {
    return page.locator(selector).first().evaluate(element => {
      const canvas = document.createElement('canvas')
      canvas.width = canvas.height = 1
      const ctx = canvas.getContext('2d', { willReadFrequently: true })
      const containers = []
      for (let parent = element; parent; parent = parent.parentElement) containers.unshift(parent)
      ctx.fillStyle = 'white'
      ctx.fillRect(0, 0, 1, 1)
      for (const parent of containers) {
        ctx.fillStyle = getComputedStyle(parent).backgroundColor
        ctx.fillRect(0, 0, 1, 1)
      }
      const background = ctx.getImageData(0, 0, 1, 1).data
      ctx.fillStyle = getComputedStyle(element).color
      ctx.fillRect(0, 0, 1, 1)
      const foreground = ctx.getImageData(0, 0, 1, 1).data
      function luminance(rgba) {
        const [r, g, b] = [...rgba].slice(0, 3).map(value => {
          const normalized = value / 255
          return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4
        })
        return r * 0.2126 + g * 0.7152 + b * 0.0722
      }
      const colors = [luminance(background), luminance(foreground)]
      return (Math.max(...colors) + 0.05) / (Math.min(...colors) + 0.05)
    })
  }
  async function widthMatrix(variant) {
    await page.locator(`.workspace[data-interface="${variant}"]`).waitFor()
    await answer.getByText(bodyText, { exact: true }).waitFor()
    for (const width of [320, 360, 390, 768, 1024, 1440]) {
      for (const fontScale of ['100', '120']) {
        await page.setViewportSize({ width, height: 850 })
        await setAppearance('studio', 'light', fontScale)
        await noOverflow(`${variant} ${width} ${fontScale}%`)
      }
    }
    for (const [preset, theme] of [['studio', 'light'], ['studio', 'dark'], ['contrast', 'light'], ['contrast', 'dark']]) {
      await setAppearance(preset, theme, '120')
      for (const selector of ['.composer textarea', '.message.assistant .markdown p', '.message.assistant .message-meta']) {
        const ratio = await contrast(selector)
        assert.ok(ratio >= 4.5, `${variant} ${preset}/${theme} ${selector}: ${ratio}`)
      }
      const iconRatio = await contrast('.message.assistant button[aria-label="Озвучить"]')
      assert.ok(iconRatio >= 3, `${variant} ${preset}/${theme} speech icon: ${iconRatio}`)
    }
    await page.setViewportSize({ width: 720, height: 450 })
    await noOverflow(`${variant} 200% effective CSS viewport`)
    await page.setViewportSize({ width: 320, height: 760 })
    await setAppearance('studio', 'light', '120')
    await composer.fill('Ввод на маленьком экране')
    await minTouch(send)
    await minTouch(accountButton)
    await minTouch(accessButton)
    await minTouch(page.getByRole('button', { name: 'Записать голос', exact: true }))
    await accessButton.focus()
    await accessButton.press('Enter')
    await history.waitFor()
    await minTouch(history.getByRole('button', { name: 'Закрыть историю' }))
    for (const key of ['Shift+Tab', 'Tab', 'Tab']) {
      await page.keyboard.press(key)
      assert.ok(await history.evaluate(dialog => dialog.contains(document.activeElement)))
    }
    await page.keyboard.press('Escape')
    await history.waitFor({ state: 'hidden' })
    assert.ok(await accessButton.evaluate(element => document.activeElement === element))
    const outlineTrigger = page.getByRole('button', { name: 'Оглавление', exact: true })
    await outlineTrigger.focus()
    await outlineTrigger.press('Space')
    const outline = page.getByRole('dialog', { name: 'Оглавление', exact: true })
    await outline.waitFor()
    await page.keyboard.press('Escape')
    await outline.waitFor({ state: 'hidden' })
    assert.ok(await outlineTrigger.evaluate(element => document.activeElement === element))
    await composer.evaluate(el => {
      el.focus()
      Object.defineProperty(window.visualViewport, 'height', { configurable: true, value: 340 })
      window.visualViewport.dispatchEvent(new Event('resize'))
    })
    await page.waitForFunction(() => {
      const button = document.querySelector('.composer .send-button').getBoundingClientRect()
      const input = document.querySelector('#composer').getBoundingClientRect()
      return button.bottom <= 341 && input.top >= 54
    })
    await page.evaluate(() => {
      delete window.visualViewport.height
      window.visualViewport.dispatchEvent(new Event('resize'))
    })
    await page.setViewportSize({ width: 640, height: 320 })
    await noOverflow(`${variant} landscape`)
    assert.ok(await answer.locator('table').evaluate(element => element.scrollWidth > element.clientWidth))
    assert.ok(await answer.locator('pre').evaluate(element => element.scrollWidth > element.clientWidth))
    assert.match(await answer.evaluate(element => getComputedStyle(element).animationDuration), /^(0s|0\.0000?1s)$/)
  }
  await widthMatrix('current')
  await accountButton.focus()
  await accountButton.press('Space')
  const firstMenuItem = page.getByRole('menuitem', { name: 'Профиль', exact: true })
  assert.ok(await firstMenuItem.evaluate(element => document.activeElement === element))
  await page.keyboard.press('Escape')
  assert.ok(await accountButton.evaluate(element => document.activeElement === element))
  await accountButton.press('Enter')
  await page.getByRole('menuitemradio', { name: 'Фокус', exact: true }).click()
  await widthMatrix('focus')

  await page.setViewportSize({ width: 320, height: 760 })
  const mic = page.getByRole('button', { name: 'Записать голос' })
  await mic.focus()
  await mic.press('Enter')
  const finish = page.getByRole('button', { name: 'Завершить запись' })
  await finish.waitFor()
  await page.waitForFunction(() => document.activeElement?.textContent.includes('Завершить запись'))
  const announced = page.locator('.composer [role="status"][aria-atomic="true"]')
  assert.match(await announced.ariaSnapshot(), /Началась запись голоса/)
  const timer = page.getByRole('timer', { name: 'Длительность записи', exact: true })
  assert.equal(await timer.getAttribute('aria-live'), 'off')
  await page.waitForFunction(() => document.querySelector('[role="timer"]')?.textContent !== '0:00')
  await finish.press('Enter')
  const listen = page.getByRole('button', { name: 'Прослушать', exact: true })
  await listen.waitFor()
  assert.match(await announced.ariaSnapshot(), /Голосовая запись готова для прослушивания/)
  await page.getByRole('button', { name: 'Удалить запись' }).click()
  await mic.waitFor()
  console.log('Verified responsive layouts, touch targets, contrast, keyboard, modal focus and accessibility descriptions for recording.')
} finally { await browser.close() }
