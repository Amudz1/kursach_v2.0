import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'

const browser = await chromium.launch({ channel: process.env.BUDZ_BROWSER || undefined, headless: true })
const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1, locale: 'ru-RU', colorScheme: 'light' })
const page = await context.newPage()
const base = process.env.BUDZ_PREVIEW ?? 'http://127.0.0.1:8080'
const captures = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../.local/ui-verification')
const failures = []
page.on('pageerror', error => failures.push(error.message))
page.setDefaultTimeout(15000)
const user = 'diploma-' + Date.now().toString(36)
const password = 'Diploma-local-qa-2026'
const write = (file) => page.screenshot({ path: path.join(captures, file), animations: 'disabled', fullPage: file.startsWith('personalization-') })
const heading = (name) => page.getByRole('heading', { name, exact: true })
const action = (name) => page.getByRole('button', { name, exact: true })
const nav = (label) => page.getByRole('navigation', { name: 'Основная навигация' }).getByRole('link', { name: label, exact: true })
const history = page.getByRole('navigation', { name: 'История диалогов' })
const lightBackground = 'rgb(243, 238, 228)'
const darkBackground = 'rgb(31, 32, 34)'
const studioPaper = 'rgb(247, 243, 234)'
const studioDark = 'rgb(24, 38, 58)'
const background = () => page.evaluate(() => getComputedStyle(document.documentElement).backgroundColor)
const tint = () => page.locator('html').getAttribute('data-tint')
const customColor = page.getByLabel('Свой цвет', { exact: true })

async function goTo(label) {
  const menu = action('Открыть меню')
  if (await menu.isVisible()) await menu.click()
  await nav(label).click()
}

async function openProfile() {
  await action('Меню аккаунта').click()
  await page.getByRole('menuitem', { name: 'Профиль', exact: true }).click()
}

async function openAppearance() {
  await action('Меню аккаунта').click()
  await page.getByRole('menuitem', { name: 'Оформление', exact: true }).click()
  await heading('Оформление').waitFor()
}

async function openChatRules() {
  await page.locator('.chat-menu > summary').click()
  await action('Правила').click()
}

async function verifyPresetContrast() {
  const results = await page.evaluate(() => {
    const root = document.documentElement
    const presetBefore = root.dataset.preset
    const themeBefore = root.dataset.theme
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = 1
    const context = canvas.getContext('2d')
    const probe = document.createElement('span')
    probe.hidden = true
    document.body.append(probe)
    function luminosity(token) {
      probe.style.color = `var(--${token})`
      context.clearRect(0, 0, 1, 1)
      context.fillStyle = getComputedStyle(probe).color
      context.fillRect(0, 0, 1, 1)
      const rgb = Array.from(context.getImageData(0, 0, 1, 1).data).slice(0, 3).map((channel) => {
        const value = channel / 255
        return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
      })
      return .2126 * rgb[0] + .7152 * rgb[1] + .0722 * rgb[2]
    }
    const pairs = [
      ['text', 'surface-0'], ['text', 'surface-1'], ['text', 'surface-2'], ['text', 'surface-3'],
      ['text-muted', 'surface-0'], ['text-muted', 'surface-1'], ['text-muted', 'surface-2'],
      ['accent', 'surface-1'], ['accent-2', 'surface-1'], ['btn-primary-text', 'btn-primary-bg'],
      ['studio-sidebar-ink', 'studio-sidebar'], ['studio-sidebar-muted', 'studio-sidebar-fill'],
      ...['danger', 'success', 'warning', 'info'].map((tone) => [`status-${tone}-text`, `status-${tone}-bg`]),
    ]
    const failures = []
    try {
      for (const preset of ['studio', 'classic', 'ultramarine', 'graphite', 'midnight', 'mint', 'sunset', 'terminal', 'contrast']) {
        root.dataset.preset = preset
        for (const mode of ['light', 'dark']) {
          root.dataset.theme = mode
          for (const [foreground, background] of pairs) {
            const values = [luminosity(foreground), luminosity(background)]
            const ratio = (Math.max(...values) + .05) / (Math.min(...values) + .05)
            const minimum = preset === 'contrast' && foreground.startsWith('text') ? 7 : 4.5
            if (ratio < minimum) failures.push(`${preset}/${mode} ${foreground} on ${background}: ${ratio.toFixed(2)} < ${minimum}`)
          }
        }
      }
    } finally {
      presetBefore ? root.dataset.preset = presetBefore : delete root.dataset.preset
      themeBefore ? root.dataset.theme = themeBefore : delete root.dataset.theme
      probe.remove()
    }
    return failures
  })
  assert.deepEqual(results, [], 'Preset contrast')
  await page.emulateMedia({ reducedMotion: 'reduce' })
  assert.equal(await page.locator('.auth-side').evaluate((element) => getComputedStyle(element, '::before').animationName), 'none')
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await page.evaluate(() => { document.documentElement.dataset.motion = 'off' })
  assert.equal(await page.locator('.auth-side').evaluate((element) => getComputedStyle(element, '::before').animationName), 'none')
  await page.evaluate(() => { document.documentElement.dataset.motion = 'full' })
}

async function captureAppearanceMatrix() {
  const matrix = path.join(captures, 'presets')
  await fs.mkdir(matrix, { recursive: true })
  const screens = [
    { id: 'appearance', ready: 'Оформление' },
    { id: 'chat', ready: 'Коллекции в C#', nav: 'Диалоги' },
    { id: 'personalization', ready: 'Персонализация', nav: 'Персонализация' },
    { id: 'plans', ready: 'Тарифы', nav: 'Тарифы' },
  ]
  const layouts = [
    { id: 'desktop', width: 1440, height: 900 },
    { id: 'mobile', width: 320, height: 820 },
  ]
  for (const [preset, label] of [['studio', 'Студия'], ['classic', 'Классическая'], ['midnight', 'Полночь'], ['mint', 'Мята']]) {
    for (const [mode, modeLabel] of [['light', 'Светлая'], ['dark', 'Тёмная']]) {
      await page.setViewportSize({ width: 1440, height: 900 })
      await openAppearance()
      await action(label).click()
      await page.getByRole('radio', { name: modeLabel, exact: true }).check()
      if (await action('Сохранить').isEnabled()) {
        await action('Сохранить').click()
        await page.getByText('Оформление сохранено.', { exact: true }).first().waitFor()
        await page.locator('button[aria-busy="true"]').waitFor({ state: 'hidden' })
      }
      const dismiss = page.getByRole('button', { name: 'Закрыть уведомление' })
      while (await dismiss.count()) {
        await dismiss.first().click()
        await page.locator('.ui-toast-leave-active').first().waitFor({ state: 'hidden' })
      }
      for (const screen of screens) {
        await page.setViewportSize({ width: 1440, height: 900 })
        if (screen.nav) await goTo(screen.nav)
        if (screen.id === 'chat') {
          if (!(await history.isVisible())) await action('Показать список диалогов').click()
          if (preset === 'studio') {
            await heading('Что хотите разобрать?').waitFor()
            await page.evaluate(() => document.fonts.ready)
            await page.screenshot({ path: path.join(matrix, `${preset}-${mode}-start-desktop.png`), animations: 'disabled' })
          }
          const thread = history.getByRole('link', { name: 'Коллекции в C#', exact: true })
          await thread.click()
          await page.getByText('Подключено', { exact: true }).waitFor()
          assert.equal(await thread.getAttribute('aria-current'), 'page')
          assert.equal(await thread.evaluate((element) => getComputedStyle(element).backgroundColor), 'rgba(0, 0, 0, 0)')
        }
        if (screen.id === 'personalization') await heading('Проверка ответа').waitFor()
        if (screen.id === 'plans') await page.getByText('Токены в день', { exact: true }).first().waitFor()
        await heading(screen.ready).waitFor()
        await page.evaluate(async () => { window.scrollTo(0, 0); await document.fonts.ready })
        for (const { id, width, height } of layouts) {
          await page.setViewportSize({ width, height })
          await noOverflow()
          if (screen.id === 'chat' && id === 'mobile') assert.ok((await page.locator('.conversation-header').boundingBox()).height <= 56)
          await page.screenshot({ path: path.join(matrix, `${preset}-${mode}-${screen.id}-${id}.png`), animations: 'disabled' })
        }
      }
    }
  }
}

async function readableColor(selector) {
  const contrast = await page.locator(selector).evaluate((element) => {
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = 1
    const context = canvas.getContext('2d')
    const luminance = (color) => {
      context.fillStyle = color
      context.fillRect(0, 0, 1, 1)
      const [red, green, blue] = Array.from(context.getImageData(0, 0, 1, 1).data).map((channel) => {
        const value = channel / 255
        return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
      })
      return 0.2126 * red + 0.7152 * green + 0.0722 * blue
    }
    const style = getComputedStyle(element)
    const values = [luminance(style.color), luminance(style.backgroundColor)]
    return (Math.max(...values) + 0.05) / (Math.min(...values) + 0.05)
  })
  assert.ok(contrast >= 4.5, `Expected readable text on ${selector}, got a ratio of ${contrast.toFixed(2)}`)
}
async function noOverflow() {
  const metrics = await page.evaluate(() => ({ actual: document.documentElement.scrollWidth, available: innerWidth }))
  assert.ok(metrics.actual <= metrics.available + 1, 'Horizontal page overflow: ' + JSON.stringify(metrics))
}
async function answerEnds(text) {
  await page.locator('.message.assistant').last().getByText(text, { exact: false }).waitFor()
}
async function mobileHistory() {
  await history.waitFor({ state: 'hidden' })
  await action('Показать список диалогов').click()
  await history.waitFor({ state: 'visible' })
  const panel = page.getByRole('dialog', { name: 'История диалогов', exact: true })
  assert.equal(await panel.evaluate((element) => element.matches(':modal') && element.contains(document.activeElement)), true)
  await panel.getByRole('button', { name: 'Новый диалог', exact: true }).waitFor()
  await page.keyboard.press('Escape')
  await history.waitFor({ state: 'hidden' })
  assert.equal(await action('Показать список диалогов').evaluate((button) => document.activeElement === button), true)
}
try {
  await fs.mkdir(captures, { recursive: true })
  await page.goto(base)
  await heading('Войти в Budz').waitFor()
  assert.equal(await background(), studioPaper)
  await verifyPresetContrast()
  await page.evaluate(() => { localStorage.setItem('budz.theme', 'system') })
  await page.getByRole('radio', { name: 'Регистрация', exact: true }).check()
  await page.getByLabel('Имя пользователя').fill(user)
  await page.getByLabel('Email', { exact: true }).fill(user + '@demo.invalid')
  await page.getByLabel('Пароль', { exact: true }).fill(password)
  await page.getByLabel('Повторите пароль').fill(password)
  await action('Создать аккаунт').click()
  await heading('Как должен выглядеть Budz?').waitFor()
  await page.getByRole('button', { name: 'Студия', pressed: true }).waitFor()
  assert.equal(await page.evaluate(() => localStorage.getItem('budz.theme')), null)
  assert.equal(await page.getByRole('radio', { name: 'Как в браузере' }).isChecked(), true)
  await page.getByRole('radio', { name: 'Тёмная' }).check()
  assert.equal(await background(), studioDark)
  await action('Дымчато-синий').click()
  await page.getByRole('button', { name: 'Дымчато-синий', pressed: true }).waitFor()
  assert.equal(await tint(), '#7293b6')
  assert.notEqual(await background(), studioDark)
  await action('Стандартный').click()
  assert.equal(await tint(), null)
  assert.equal(await background(), studioDark)
  await action('Далее').click()
  await heading('Как вам отвечать?').waitFor()
  await page.getByLabel('Язык', { exact: true }).selectOption('ru')
  await page.getByLabel('Формат', { exact: true }).selectOption('code')
  await action('Далее').click()
  await page.getByLabel('Интересы').fill('C#, PostgreSQL')
  await page.getByLabel('О себе').fill('Изучаю .NET и предпочитаю примеры из практики.')
  await action('Далее').click()
  await heading('Разрешить автоматическую память?').waitFor()
  await action('Начать работу').click()
  await heading('Что хотите разобрать?').waitFor()
  assert.equal(await page.getByLabel('Текущая персона').count(), 0)
  const topNavigation = page.locator('header').getByRole('navigation', { name: 'Основная навигация' })
  const headerNav = await topNavigation.boundingBox()
  assert.ok(headerNav.y < 70 && Math.abs(headerNav.x + headerNav.width / 2 - 720) < 1, 'Navigation must be centered in the desktop header')
  assert.equal(await page.getByRole('navigation', { name: 'Основная навигация' }).count(), 1)
  const fence = String.fromCharCode(96).repeat(3)
  const query = 'Объясни коллекции C#.\n\n' + fence + 'csharp\nvar books = new List<string>();\n' + fence
  const composer = page.getByLabel('Сообщение для Budz')
  await action('Составить план').click()
  assert.equal(await composer.inputValue(), 'Составить план')
  await composer.fill('')
  await composer.blur()
  await page.mouse.move(10, 10)
  await write('studio-start-dark.png')
  const compact = (await composer.boundingBox()).height
  assert.ok(compact <= 40, 'An empty composer must fit on one line')
  await composer.fill('Строка\n'.repeat(4))
  assert.ok((await composer.boundingBox()).height > compact, 'Composer must grow when typing')
  await composer.fill('Строка\n'.repeat(30))
  const full = await composer.evaluate((element) => ({ height: element.clientHeight, scroll: element.scrollHeight }))
  assert.ok(full.height <= 142 && full.scroll > full.height, 'Composer must scroll at its height limit')
  await composer.fill('')
  assert.equal((await composer.boundingBox()).height, compact)
  await composer.fill(query)
  const sentAt = performance.now()
  await page.getByRole('button', { name: 'Отправить' }).click()
  await page.waitForURL('**/chats/*')
  assert.equal(await nav('Диалоги').getAttribute('aria-current'), 'location')
  await page.locator('.message.assistant .markdown').first().waitFor()
  assert.ok(performance.now() - sentAt < 3000, 'Demo provider first chunk exceeded the 3-second target')
  await action('Остановить').click()
  const userCode = page.locator('.message.user .code-frame').first()
  await userCode.getByRole('button', { name: 'Номера строк' }).click()
  assert.equal(await userCode.getByRole('button', { name: 'Номера строк' }).getAttribute('aria-pressed'), 'true')
  assert.equal(await userCode.locator('.code-gutter').textContent(), '1')
  await userCode.getByRole('button', { name: 'Номера строк' }).click()
  await answerEnds('Генерация остановлена')
  await action('Повторить').click()
  await answerEnds('Демонстрационный ответ')
  await page.locator('.message.assistant pre code').waitFor()
  await page.locator('.chat-menu > summary').click()
  await action('Переименовать').click()
  await page.getByRole('dialog').getByLabel('Название').fill('Коллекции в C#')
  await page.getByRole('dialog').getByRole('button', { name: 'Сохранить' }).click()
  await heading('Коллекции в C#').waitFor()
  await action('Полезно').click()
  await page.getByRole('button', { name: 'Полезно', pressed: true }).waitFor()
  const chat = page.url()
  const conversation = page.locator('.conversation')
  const expandedWidth = (await conversation.boundingBox()).width
  await composer.fill('Черновик для проверки')
  await action('Скрыть список диалогов').click()
  await history.waitFor({ state: 'hidden' })
  assert.ok((await conversation.boundingBox()).width > expandedWidth)
  assert.equal(await composer.inputValue(), 'Черновик для проверки')
  assert.equal(page.url(), chat)
  await action('Показать список диалогов').waitFor()
  await composer.fill('')
  await page.reload()
  await heading('Коллекции в C#').waitFor()
  await action('Показать список диалогов').waitFor()
  await history.waitFor({ state: 'hidden' })
  assert.equal(await background(), studioDark)
  await page.getByText('Подключено', { exact: true }).waitFor()
  await write('chat-dark.png')
  await action('Показать список диалогов').click()
  await history.getByText('Коллекции в C#', { exact: true }).waitFor()
  await openProfile()
  await page.getByRole('link', { name: 'Оформление', exact: true }).click()
  await action('Классическая').click()
  await page.getByRole('radio', { name: 'Светлая' }).check()
  await page.emulateMedia({ colorScheme: 'dark' })
  assert.equal(await background(), lightBackground)
  await page.getByRole('radio', { name: 'Как в браузере' }).check()
  assert.equal(await background(), darkBackground)
  await customColor.fill('#ffffff')
  assert.equal(await tint(), '#ffffff')
  assert.notEqual(await background(), darkBackground)
  await readableColor('html')
  await readableColor('.account-link .avatar')
  await customColor.fill('#806f9e')
  assert.equal(await tint(), '#806f9e')
  const customDarkBackground = await background()
  await action('Сохранить').click()
  await page.getByText('Оформление сохранено.', { exact: true }).waitFor()
  await page.reload()
  await heading('Оформление').waitFor()
  assert.equal(await customColor.inputValue(), '#806f9e')
  assert.equal(await background(), customDarkBackground)
  await noOverflow()
  await write('appearance-classic.png')
  await page.emulateMedia({ colorScheme: 'light' })
  assert.equal(await tint(), '#806f9e')
  assert.notEqual(await background(), customDarkBackground)
  assert.notEqual(await background(), lightBackground)
  await customColor.fill('#000000')
  await readableColor('html')
  await readableColor('.account-link .avatar')
  await page.getByRole('radio', { name: 'Стандартный', exact: true }).check()
  assert.equal(await tint(), null)
  assert.equal(await background(), lightBackground)
  await action('Полночь').click()
  await action('Сохранить').click()
  await page.getByText('Оформление сохранено.', { exact: true }).waitFor()
  await page.reload()
  await heading('Оформление').waitFor()
  assert.equal(await page.locator('html').getAttribute('data-preset'), 'midnight')
  const otherContext = await browser.newContext({ locale: 'ru-RU' })
  try {
    const otherPage = await otherContext.newPage()
    await otherContext.addInitScript(() => {
      if (!localStorage.getItem('budz.appearance')) localStorage.setItem('budz.theme', 'light')
    })
    await otherPage.goto(base)
    await otherPage.getByLabel('Логин или email').fill(user)
    await otherPage.getByLabel('Пароль', { exact: true }).fill(password)
    await otherPage.getByRole('button', { name: 'Войти', exact: true }).click()
    await otherPage.waitForURL('**/chats')
    assert.equal(await otherPage.locator('html').getAttribute('data-preset'), 'midnight')
    assert.equal(await otherPage.evaluate(() => localStorage.getItem('budz.theme')), null)
  } finally { await otherContext.close() }
  await write('appearance-midnight.png')
  await action('Сбросить к стандартным').click()
  await page.getByText('Стандартное оформление восстановлено.', { exact: true }).waitFor()
  await page.goto(chat)
  await heading('Коллекции в C#').waitFor()
  await page.getByText('Подключено', { exact: true }).waitFor()
  await noOverflow()
  await write('chat-desktop.png')

  await goTo('Персонализация')
  await heading('Проверка ответа').waitFor()
  await action('Редактор').click()
  assert.equal(await page.getByLabel('Длина ответа').inputValue(), 'short')
  await page.getByText('Коллекция хранит группу элементов.', { exact: true }).waitFor()
  await action('Отменить изменения').click()
  assert.equal(await page.getByLabel('Формат', { exact: true }).inputValue(), 'code')
  assert.equal(await action('Сохранить настройки').isEnabled(), false)
  await action('Наставник').click()
  assert.equal(await page.getByLabel('Длина ответа').inputValue(), 'long')
  await page.getByLabel('Язык', { exact: true }).selectOption('en')
  await page.getByText('What is a collection?', { exact: true }).waitFor()
  await page.getByLabel('Язык', { exact: true }).selectOption('ru')
  await page.getByRole('tab', { name: 'Контекст и память', exact: true }).click()
  await page.getByLabel('Текущая цель').fill('Подготовить диплом по C#')
  await page.getByLabel('Использовать сохранённые факты в ответах').uncheck()
  await page.getByRole('tab', { name: 'Правила задач', exact: true }).click()
  await page.getByLabel('Длина для задачи').selectOption('short')
  await action('Сбросить правило задачи').click()
  assert.equal(await page.getByLabel('Длина для задачи').inputValue(), '')
  await page.getByLabel('Длина для задачи').selectOption('short')
  await page.getByLabel('Формат для задачи').selectOption('code')
  await page.getByLabel('Инструкция для задачи').fill('Проверяй пограничные случаи')
  await action('Свой запрос').click()
  await page.getByLabel('Ваш запрос').fill('А подробнее?')
  await page.getByText('Добавить предыдущий запрос', { exact: true }).click()
  await page.getByLabel('Предыдущий запрос').fill('Проверь мою функцию на C#')
  await action('Проверить правила').click()
  await page.getByText('Тип задачи определён по предыдущему запросу.', { exact: true }).waitFor()
  await page.getByText('Коротко · С примерами кода', { exact: true }).waitFor()
  await page.getByText('Проверяй пограничные случаи', { exact: true }).waitFor()
  await noOverflow()
  await page.evaluate(() => window.scrollTo(0, 0))
  await write('personalization-desktop.png')
  await action('Сохранить настройки').click()
  await page.getByText('Настройки сохранены.', { exact: false }).waitFor()

  await page.setViewportSize({ width: 768, height: 900 })
  await noOverflow()
  await goTo('Диалоги')
  await noOverflow()
  await mobileHistory()
  await page.setViewportSize({ width: 360, height: 800 })
  await page.goto(chat)
  await heading('Коллекции в C#').waitFor()
  await noOverflow()
  await page.getByRole('button', { name: 'Прикрепить' }).waitFor()
  await page.getByText('Подключено', { exact: true }).waitFor()
  await write('chat-mobile.png')
  await mobileHistory()
  await goTo('Персонализация')
  await heading('Проверка ответа').waitFor()
  assert.equal(await page.getByLabel('Длина ответа').inputValue(), 'long')
  const mobileHeader = await page.locator('.topbar').boundingBox()
  assert.ok(mobileHeader.height <= 56, 'Mobile header must fit within 56 pixels')
  await topNavigation.waitFor({ state: 'hidden' })
  await action('Открыть меню').click()
  const drawer = page.getByRole('dialog', { name: 'Меню' })
  await drawer.waitFor()
  assert.equal(await drawer.evaluate((element) => element.contains(document.activeElement)), true)
  await drawer.getByRole('button', { name: 'Выйти' }).focus()
  await page.keyboard.press('Tab')
  assert.equal(await drawer.getByRole('button', { name: 'Закрыть меню' }).evaluate((element) => element === document.activeElement), true)
  await write('drawer-mobile.png')
  await page.keyboard.press('Escape')
  await drawer.waitFor({ state: 'hidden' })
  assert.equal(await action('Открыть меню').evaluate((element) => element === document.activeElement), true)
  await action('Открыть меню').click()
  await page.mouse.click(350, 400)
  await drawer.waitFor({ state: 'hidden' })
  await page.getByRole('link', { name: 'К проверке ↓' }).click()
  await page.getByText('Что такое коллекция?', { exact: true }).waitFor()
  await page.getByRole('tab', { name: 'Контекст и память', exact: true }).click()
  assert.equal(await page.getByLabel('Текущая цель').inputValue(), 'Подготовить диплом по C#')
  assert.equal(await page.getByLabel('Использовать сохранённые факты в ответах').isChecked(), false)
  await page.getByRole('tab', { name: 'Стиль ответа', exact: true }).click()
  await noOverflow()
  await page.evaluate(() => window.scrollTo(0, 0))
  await write('personalization-mobile.png')

  await page.evaluate(() => { document.documentElement.style.zoom = '2' })
  await page.setViewportSize({ width: 768, height: 1000 })
  await noOverflow()
  await page.evaluate(() => { document.documentElement.style.zoom = '' })
  await page.locator('body').click({ position: { x: 0, y: 0 } })
  await page.keyboard.press('Tab')
  assert.ok(await page.evaluate(() => document.activeElement?.matches(':focus-visible')), 'Keyboard focus must be visible')

  await goTo('Память')
  await action('Добавить факт').click()
  await heading('Добавить факт').waitFor()
  await page.getByLabel('Что запомнить').fill('Изучаю тестирование C#')
  await action('Сохранить факт').click()
  await page.getByText('Изучаю тестирование C#', { exact: true }).waitFor()
  await goTo('Тарифы')
  await heading('Тарифы').waitFor()
  await page.getByText('Токены в день', { exact: true }).first().waitFor()
  await action('Сравнить тарифы').click()
  await page.getByRole('table').waitFor()
  await action('К списку тарифов').click()
  await page.getByRole('button', { name: 'Подключить', exact: true }).first().click()
  await action('Заполнить тестовыми данными').click()
  await action('Подтвердить').click()
  await page.getByText('Оплата была демонстрационной', { exact: false }).waitFor()

  await page.setViewportSize({ width: 1440, height: 900 })
  await goTo('Диалоги')
  await heading('Проекты').waitFor()
  await heading('Все диалоги').waitFor()
  assert.equal(await nav('Пространства').count(), 0)
  assert.equal(await nav('Проекты').count(), 0)
  await action('Новый проект').click()
  const editor = page.getByRole('dialog', { name: 'Новый проект' })
  await editor.getByLabel('Название', { exact: true }).fill('Диплом по C#')
  await editor.getByLabel('Инструкции проекта').fill('Покажи, как проверить пример перед защитой.')
  await editor.getByLabel('Материалы проекта').fill('Материалы диплома о потоковых ответах.')
  await action('Сохранить проект').click()
  await page.locator('.conversation-heading').getByText('Диплом по C#', { exact: true }).waitFor()
  const projects = page.getByRole('navigation', { name: 'Список проектов' })
  const project = projects.getByRole('link', { name: 'Диплом по C#', exact: true })
  await project.waitFor()
  await action('Новый проект').click()
  await editor.getByLabel('Название', { exact: true }).fill('Черновики')
  await action('Сохранить проект').click()
  await page.locator('.conversation-heading').getByText('Черновики', { exact: true }).waitFor()
  await project.click()
  await page.locator('.conversation-heading').getByText('Диплом по C#', { exact: true }).waitFor()
  assert.equal(await project.getAttribute('aria-current'), 'location')
  assert.equal(await projects.getByRole('link', { name: 'Черновики', exact: true }).getAttribute('aria-current'), null)
  assert.ok((await projects.boundingBox()).y < (await history.boundingBox()).y, 'Projects must appear above the complete history')
  await history.getByText('Коллекции в C#', { exact: true }).waitFor()
  await page.getByText('В проекте пока нет диалогов.', { exact: true }).waitFor()
  await openChatRules()
  const rules = page.getByRole('dialog', { name: 'Правила диалога' })
  await rules.getByText('Покажи, как проверить пример перед защитой.', { exact: true }).waitFor()
  await rules.getByText('Выключена', { exact: true }).waitFor()
  await action('Закрыть').click()
  await composer.fill('Проверь функцию на C#')
  await action('Отправить').click()
  await action('Правила ответа').waitFor()
  await action('Правила ответа').click()
  await rules.getByText('Настройки, использованные для этого ответа.', { exact: true }).waitFor()
  await rules.getByText('Учтены', { exact: true }).waitFor()
  await write('chat-rules.png')
  await action('Закрыть').click()
  await page.locator('.chat-menu > summary').click()
  await page.getByRole('link', { name: 'Экспорт диалога' }).click()
  await heading('Управление данными').waitFor()
  const [archive] = await Promise.all([page.waitForEvent('download'), action('Скачать JSON').click()])
  const exported = JSON.parse(await fs.readFile(await archive.path(), 'utf-8'))
  assert.equal(exported.format, 'budz-conversations')
  assert.equal(exported.conversations[0].messages[0].content, 'Проверь функцию на C#')
  const [markdown] = await Promise.all([page.waitForEvent('download'), action('Скачать Markdown').click()])
  assert.match(await fs.readFile(await markdown.path(), 'utf-8'), /### Пользователь/u)
  await page.getByRole('radio', { name: 'Импорт', exact: true }).check()
  const foreign = [{ name: 'Диалог из Claude', uuid: 'qa-claude', chat_messages: [
    { sender: 'human', text: 'Подскажи, как проверить диплом.' }, { sender: 'assistant', text: '## План проверки\n\nНачни с матрицы приёмки.' },
  ] }]
  await page.getByLabel('Файл переписки').setInputFiles({ name: 'conversations.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(foreign)) })
  await action('Импортировать').click()
  await page.getByText('Импортировано 1.', { exact: false }).waitFor()
  await write('data-transfer.png')
  await page.getByRole('link', { name: 'Открыть диалог', exact: true }).click()
  await heading('Диалог из Claude').waitFor()
  await page.getByText('Начни с матрицы приёмки.', { exact: true }).waitFor()
  await page.locator('.message.assistant .markdown').getByRole('heading', { name: 'План проверки', level: 2 }).waitFor()
  await history.getByText('Диалог из Claude', { exact: true }).waitFor()
  await project.click()
  await heading('Диалоги проекта').waitFor()
  const projectHistory = page.getByRole('navigation', { name: 'Диалоги проекта' })
  await projectHistory.getByText('Диалог из Claude', { exact: true }).waitFor()
  assert.equal(await projectHistory.getByText('Коллекции в C#', { exact: true }).count(), 0)
  await history.getByText('Коллекции в C#', { exact: true }).waitFor()
  await history.getByText('Диалог из Claude', { exact: true }).waitFor()
  await write('workspaces.png')
  await goTo('Диалоги')
  await heading('Что хотите разобрать?').waitFor()
  await history.getByText('Диалог из Claude', { exact: true }).click()
  await heading('Диалог из Claude').waitFor()
  await page.setViewportSize({ width: 360, height: 800 })
  await noOverflow()
  await openChatRules()
  await rules.getByText('Покажи, как проверить пример перед защитой.', { exact: true }).waitFor()
  await action('Закрыть').click()
  await action('Показать список диалогов').click()
  await write('projects-mobile.png')
  await project.click()
  await heading('Диалоги проекта').waitFor()
  await history.waitFor({ state: 'hidden' })
  await projectHistory.getByText('Диалог из Claude', { exact: true }).waitFor()

  await page.goto(`${base}/data`)
  await heading('Управление данными').waitFor()
  await noOverflow()
  await action('Открыть временный диалог').click()
  await page.getByText('Временный диалог', { exact: true }).waitFor()
  assert.equal(await action('Прикрепить').count(), 0)
  await openChatRules()
  await rules.getByText('Выключена', { exact: true }).waitFor()
  await action('Закрыть').click()

  if (process.env.BUDZ_VISUAL === '1') await captureAppearanceMatrix()
  await openProfile()
  await heading('Личная статистика').waitFor()
  await page.setViewportSize({ width: 360, height: 800 })
  await noOverflow()
  await page.getByText('Запросов за период', { exact: true }).waitFor()
  await write('profile-mobile.png')
  await action('Удалить аккаунт').click()
  await page.getByLabel('Подтвердите паролем').fill(password)
  await page.getByRole('dialog').getByRole('button', { name: 'Удалить аккаунт', exact: true }).click()
  await heading('Войти в Budz').waitFor()
  assert.deepEqual(failures, [], 'Unhandled browser exceptions')
  console.log('Verified Vue: projects above complete history, project navigation on desktop and mobile, response rules, imports and exports, temporary chat and themes.')
} catch (error) {
  console.error('Debug state:', await page.locator('body').innerText().catch(() => ''), failures)
  await page.screenshot({ path: path.join(captures, 'failed.png') }).catch(() => undefined)
  throw error
} finally {
  await browser.close()
}
