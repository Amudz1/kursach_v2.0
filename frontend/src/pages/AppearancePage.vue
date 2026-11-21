<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch, watchPostEffect } from 'vue'
import { Download, Palette, RotateCcw, Upload } from 'lucide-vue-next'
import { explain } from '../api'
import {
  appearanceDocument, appearanceOptions, appearanceSchemaVersion, cancelPreview, normalizeAppearance, presets, presetSettings,
  previewAppearance, resetAppearance, saveAccountAppearance, savedAppearance, type AppearanceSettings, type PresetId,
} from '../appearance'
import { adjustForContrast, hexContrast, parseCssColor, toHex } from '../appearance/color'
import { accentSwatches as swatches } from '../appearance/swatches'
import ChatMarkdown from '../components/ChatMarkdown.vue'
import UiAvatar from '../components/ui/UiAvatar.vue'
import UiBadge from '../components/ui/UiBadge.vue'
import UiButton from '../components/ui/UiButton.vue'
import UiColorPicker from '../components/ui/UiColorPicker.vue'
import { useToast } from '../components/ui/use-toast'

const draft = ref<AppearanceSettings>({ ...savedAppearance.value })
const toast = useToast()
const busy = ref(false)
const error = ref('')
const importFile = ref<HTMLInputElement>()
const previewSurface = ref<HTMLElement>()
const contrastWarning = ref<{ key: 'accent' | 'accent2'; ratio: number; correction: string | null }[]>([])
const prefersContrast = window.matchMedia('(prefers-contrast: more)').matches
function matches(settings: AppearanceSettings, reference: Readonly<AppearanceSettings>) {
  return (Object.keys(settings) as (keyof AppearanceSettings)[]).every((key) => settings[key] === reference[key])
}
const dirty = computed(() => !matches(draft.value, savedAppearance.value))
const modified = computed(() => !matches(draft.value, presetSettings(draft.value.preset)))
const secondSwatches = [{ value: 'auto', label: 'Автоматический второй акцент', css: 'var(--accent-2)' }, ...swatches.slice(0, 8)]
const colorKeys = ['accent', 'accent2', 'assistantAvatarColor'] as const
type SelectKey = Exclude<keyof typeof appearanceOptions, 'preset'>
type SelectField = { key: SelectKey; label: string; names: string[] }
const fields: { title: string; items: SelectField[] }[] = [
  { title: 'Форма и плотность', items: [
    { key: 'radius', label: 'Скругление', names: ['Прямое', 'Стандартное', 'Мягкое', 'Округлое'] },
    { key: 'density', label: 'Плотность', names: ['Компактная', 'Обычная', 'Просторная'] },
  ] },
  { title: 'Шрифты', items: [
    { key: 'font', label: 'Шрифт интерфейса', names: ['Системный', 'Inter', 'Manrope', 'IBM Plex Sans'] },
    { key: 'fontScale', label: 'Размер текста', names: ['90%', '100%', '110%', '120%'] },
    { key: 'codeFont', label: 'Шрифт кода', names: ['Системный', 'JetBrains Mono', 'Fira Code'] },
  ] },
  { title: 'Диалоги', items: [
    { key: 'messageStyle', label: 'Вид сообщений', names: ['Пузыри', 'Карточки', 'Лента'] },
    { key: 'chatWidth', label: 'Ширина ленты', names: ['Узкая', 'Средняя', 'Широкая'] },
    { key: 'userSide', label: 'Свои сообщения', names: ['Слева', 'Справа'] },
    { key: 'assistantAvatar', label: 'Аватар помощника', names: ['Иконка персоны', 'Логотип Budz', 'Свой цвет'] },
    { key: 'codeTheme', label: 'Тема кода', names: ['Классическая', 'GitHub', 'Atom One', 'Tokyo Night'] },
    { key: 'messageActions', label: 'Действия сообщения', names: ['Всегда', 'При наведении'] },
  ] },
  { title: 'Фон и анимация', items: [
    { key: 'pattern', label: 'Узор фона', names: ['Нет', 'Точки', 'Сетка', 'Шум', 'Облака'] },
    { key: 'motion', label: 'Анимация', names: ['Обычная', 'Уменьшенная', 'Выключена'] },
  ] },
]
const codeExample = 'Пример на C#\n\n```csharp\nConsole.WriteLine("Привет, Budz!");\n```'

function update<K extends keyof AppearanceSettings>(key: K, value: unknown) {
  draft.value = normalizeAppearance({ ...draft.value, [key]: value })
}
function selectValue(key: SelectKey, event: Event) {
  const value = (event.target as HTMLSelectElement).value
  update(key, key === 'fontScale' ? Number(value) : value)
}
function choosePreset(id: PresetId) { draft.value = presetSettings(id) }
function cancel() { draft.value = { ...savedAppearance.value }; error.value = '' }
watch(draft, (settings) => previewAppearance(settings), { flush: 'sync' })
watch(savedAppearance, (settings, previous) => {
  if (matches(draft.value, previous)) draft.value = { ...settings }
})
watchPostEffect(() => {
  const surface = previewSurface.value
  const colors = draft.value
  const background = surface && parseCssColor(getComputedStyle(surface).backgroundColor)
  contrastWarning.value = background ? colorKeys.slice(0, 2).flatMap((value) => {
    const key = value as 'accent' | 'accent2'
    const selected = colors[key]
    if (!selected || selected === 'auto') return []
    const ratio = hexContrast(selected, toHex(background))
    return ratio >= 4.5 ? [] : [{ key, ratio, correction: adjustForContrast(selected, toHex(background)) }]
  }) : []
})
onBeforeUnmount(cancelPreview)

async function save() {
  busy.value = true
  error.value = ''
  try {
    await saveAccountAppearance(draft.value)
    cancel()
    toast.success('Оформление сохранено.')
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
async function reset() {
  busy.value = true
  error.value = ''
  try {
    await resetAppearance()
    cancel()
    toast.success('Стандартное оформление восстановлено.')
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
function download() {
  const url = URL.createObjectURL(new Blob([JSON.stringify(appearanceDocument(draft.value), null, 2)], { type: 'application/json' }))
  const link = document.createElement('a')
  link.href = url
  link.download = 'budz-appearance.json'
  link.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}
async function importTheme(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  error.value = ''
  try {
    if (file.size > 8192) throw new Error()
    const data = JSON.parse(await file.text())
    if (!data || data.schemaVersion !== appearanceSchemaVersion || !appearanceOptions.preset.includes(data.preset)
      || !appearanceOptions.mode.includes(data.colorMode) || !data.settings || typeof data.settings !== 'object' || Array.isArray(data.settings)) throw new Error()
    const preset = data.preset as PresetId
    const raw = { ...presetSettings(preset), ...data.settings, preset, mode: data.colorMode }
    const validated = normalizeAppearance(raw)
    for (const [key, value] of Object.entries(data.settings)) {
      if (key === 'preset' || key === 'mode' || !Object.hasOwn(validated, key)
        || validated[key as keyof AppearanceSettings] !== (typeof value === 'string' && value.startsWith('#') ? value.toLowerCase() : value)) throw new Error()
    }
    draft.value = validated
    toast.info('Тема загружена в предпросмотр.')
  } catch { error.value = 'Выберите корректный JSON оформления Budz объёмом до 8 КБ.' }
}
</script>

<template>
  <section class="page appearance-page">
    <header class="page-heading"><h1>Оформление</h1><p>Настройте интерфейс Budz. Сохранённые настройки доступны в вашем аккаунте.</p></header>
    <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
    <div class="appearance-grid">
      <form class="stack" @submit.prevent="save">
        <fieldset class="fieldset stack" :disabled="busy">
          <legend>Тема</legend>
          <div class="preset-grid">
            <button v-for="preset in appearanceOptions.preset" :key="preset" type="button" class="preset-button"
              :data-choice="preset" :aria-pressed="preset === draft.preset" @click="choosePreset(preset)">
              <span class="preset-sample" aria-hidden="true"><i></i><i></i><i></i></span>
              {{ presets[preset].label }}
            </button>
          </div>
          <p v-if="modified" class="help">Пресет изменён</p>
          <UiButton v-if="prefersContrast && draft.preset !== 'contrast'" @click="choosePreset('contrast')">Выбрать высокий контраст</UiButton>
          <div class="mode-group" role="group" aria-label="Цветовой режим">
            <label v-for="(mode, index) in appearanceOptions.mode" :key="mode" class="check">
              <input type="radio" name="appearance-mode" :value="mode" :checked="draft.mode === mode" @change="update('mode', mode)">
              {{ ['Как в браузере', 'Светлая', 'Тёмная'][index] }}
            </label>
          </div>
        </fieldset>
        <fieldset class="fieldset stack" :disabled="busy">
          <legend>Цвет и свечение</legend>
          <UiColorPicker :model-value="draft.accent" label="Основной акцент" :swatches="swatches" custom none-label="Стандартный"
            @update:model-value="update('accent', $event)" />
          <UiColorPicker :model-value="draft.accent2" label="Второй акцент" :swatches="secondSwatches" custom
            custom-label="Свой второй цвет" none-label="Второй цвет пресета" @update:model-value="update('accent2', $event)" />
          <p v-for="entry in contrastWarning" :key="entry.key" class="notice" role="status">
            {{ entry.key === 'accent' ? 'Основной акцент' : 'Второй акцент' }}: контраст {{ entry.ratio.toFixed(2) }}:1.
            <button v-if="entry.correction" type="button" class="text-button" @click="update(entry.key, entry.correction)">Исправить цвет</button>
          </p>
          <label>Оттенок фона · {{ draft.backgroundTint }}%
            <input type="range" min="0" max="30" :value="draft.backgroundTint"
              @input="update('backgroundTint', Number(($event.target as HTMLInputElement).value))">
          </label>
          <label>Свечение
            <select :value="draft.glow" @change="selectValue('glow', $event)">
              <option value="off">Выкл.</option><option value="soft">Мягкое</option><option value="bright">Яркое</option>
            </select>
          </label>
        </fieldset>
        <fieldset v-for="group in fields" :key="group.title" class="fieldset stack" :disabled="busy">
          <legend>{{ group.title }}</legend>
          <div class="form-grid">
            <label v-for="field in group.items" :key="field.key">{{ field.label }}
              <select :value="draft[field.key]" @change="selectValue(field.key, $event)">
                <option v-for="(option, index) in appearanceOptions[field.key]" :key="option" :value="option">{{ field.names[index] }}</option>
              </select>
            </label>
          </div>
          <UiColorPicker v-if="group.title === 'Диалоги' && draft.assistantAvatar === 'custom'" :model-value="draft.assistantAvatarColor"
            label="Цвет аватара" :swatches="swatches" custom custom-label="Свой цвет аватара" @update:model-value="update('assistantAvatarColor', $event)" />
        </fieldset>
        <div class="actions">
          <UiButton type="submit" variant="primary" :loading="busy" :disabled="!dirty">Сохранить</UiButton>
          <UiButton :disabled="!dirty || busy" @click="cancel">Отменить</UiButton>
          <UiButton :icon="RotateCcw" :disabled="busy" @click="reset">Сбросить к стандартным</UiButton>
          <UiButton :icon="Download" @click="download">Экспорт темы</UiButton>
          <UiButton :icon="Upload" :disabled="busy" @click="importFile?.click()">Импорт темы</UiButton>
          <input ref="importFile" type="file" hidden accept=".json,application/json" @change="importTheme">
        </div>
      </form>
      <aside ref="previewSurface" class="preview-surface stack" aria-labelledby="preview-title">
        <div class="actions"><Palette aria-hidden="true" /><h2 id="preview-title">Предпросмотр</h2></div>
        <div class="preview-message user" :class="[draft.messageStyle, draft.userSide]">Покажи короткий пример на C#.</div>
        <div class="preview-message assistant" :class="draft.messageStyle">
          <div class="actions"><UiAvatar name="Budz" brand decorative /><strong>Budz</strong></div>
          <ChatMarkdown :content="codeExample" />
        </div>
        <div class="actions">
          <UiButton variant="primary">Продолжить</UiButton><UiBadge tone="info">Пример</UiBadge>
        </div>
      </aside>
    </div>
  </section>
</template>

<style scoped>
.appearance-grid { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr); gap: var(--space-6); align-items: start; }
.preview-surface { position: sticky; top: calc(var(--header) + var(--space-4)); padding: var(--space-4);
  border: 1px solid var(--border); border-radius: var(--radius-lg); color: var(--text); background: var(--surface-1); }
.preview-surface h2 { font-size: var(--font-size-xl); }
.preset-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(9rem, 100%), 1fr)); gap: var(--space-2); }
.preset-button { display: grid; gap: var(--space-2); justify-items: start; text-align: start; padding: var(--space-2);
  font-size: var(--font-size-xs); color: var(--text); background: var(--surface-1); border: 1px solid var(--border); border-radius: var(--radius-md); }
.preset-button[aria-pressed="true"] { border-color: var(--accent); box-shadow: inset 0 0 0 1px var(--accent); font-weight: 700; }
.preset-button:hover { background: var(--surface-2); }
.preset-sample { display: grid; gap: 3px; align-content: center; width: 100%; height: 34px; padding: 4px 8px;
  background: var(--sample-bg, var(--surface-3)); border-radius: var(--radius-xs); }
.preset-sample i { display: block; width: 45%; height: 3px; border-radius: var(--radius-full); background: var(--sample-line, var(--accent)); }
.preset-sample i:nth-child(2) { width: 70%; }
.preset-sample i:nth-child(3) { width: 30%; }
[data-choice="studio"] { --sample-bg: var(--studio-sand); --sample-line: var(--studio-terracotta); }
[data-choice="classic"] { --sample-bg: var(--classic-paper); --sample-line: var(--classic-accent); }
[data-choice="ultramarine"] { --sample-bg: var(--c-brand-100); --sample-line: var(--c-brand-600); }
[data-choice="graphite"] { --sample-bg: var(--c-ink-800); --sample-line: yellowgreen; }
[data-choice="midnight"] { --sample-bg: var(--c-brand-900); --sample-line: lightblue; }
[data-choice="mint"] { --sample-bg: honeydew; --sample-line: var(--cat-5); }
[data-choice="sunset"] { --sample-bg: mistyrose; --sample-line: var(--cat-6); }
[data-choice="terminal"] { --sample-bg: black; --sample-line: lightgreen; }
[data-choice="contrast"] { --sample-bg: white; --sample-line: black; }
.mode-group { display: flex; flex-wrap: wrap; gap: var(--space-3); }
.mode-group label { min-height: 40px; }
.mode-group input { width: 16px; height: 16px; margin: 0; flex: 0 0 auto; accent-color: var(--accent); }
.preview-message { display: grid; gap: var(--space-2); padding: var(--space-3); border: 1px solid var(--border); border-radius: var(--radius-md); }
.preview-message.user { background: var(--message-user-bg); }
.preview-message.right { margin-inline-start: auto; }
.preview-message.bubbles { width: fit-content; max-width: 100%; border-radius: var(--radius-xl); }
.preview-message.flat { background: transparent; border-width: 0 0 1px; border-radius: 0; }
:global(:root[data-preset="studio"] .preview-message.flat.user) {
  background: var(--message-user-bg); border: 0; border-radius: var(--radius-lg) var(--radius-lg) var(--radius-xs) var(--radius-lg);
}
@media (max-width: 850px) {
  .appearance-grid { grid-template-columns: minmax(0, 1fr); }
  .preview-surface { position: static; grid-row: 1; }
}
</style>
