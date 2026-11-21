<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { Eye, Moon, Sun, Type } from 'lucide-vue-next'
import { adjustForContrast, hexContrast } from '../../src/appearance/color'
import { loadFonts } from '../../src/appearance/fonts'
import { readHex } from '../../src/appearance/model'
import ChatMarkdown from '../../src/components/ChatMarkdown.vue'
import UiButton from '../../src/components/ui/UiButton.vue'
import UiSelect from '../../src/components/ui/UiSelect.vue'
import { paperStyles, readingStyles, studioColors, type Mode, type ReadingStyle } from './reading-style'
import { previewAnswer } from './sample-data'

const props = defineProps<{ saved: ReadingStyle; mode: Mode; showDraft: boolean }>()
const emit = defineEmits<{ save: [value: ReadingStyle] }>()
const draft = ref<ReadingStyle>({ ...props.saved, ...(props.showDraft ? { headingColor: 'custom' } : {}) })
const previewMode = ref<Mode>(props.mode)
const preview = ref<HTMLElement>()
const previewShortcut = ref<HTMLButtonElement>()
const status = ref('')
let lastField: HTMLElement | undefined
const changed = computed(() => (Object.keys(draft.value) as (keyof ReadingStyle)[]).some(key => draft.value[key] !== props.saved[key]))
const colors = computed(() => (['light', 'dark'] as const).map(mode => {
  const key: 'lightHeading' | 'darkHeading' = mode === 'light' ? 'lightHeading' : 'darkHeading'
  const ratio = hexContrast(draft.value[key], studioColors[mode].paper)
  return {
    key, mode, ratio,
    label: mode === 'light' ? 'Светлая тема' : 'Тёмная тема',
    suggestion: ratio < 4.5 ? adjustForContrast(draft.value[key], studioColors[mode].paper) : null,
  }
}))
const valid = computed(() => draft.value.headingColor !== 'custom' || colors.value.every(color => color.ratio >= 4.5))
const fonts = [
  { value: 'inherit', label: 'Как в интерфейсе' },
  { value: 'plex', label: 'IBM Plex Sans' },
  { value: 'inter', label: 'Inter' },
  { value: 'manrope', label: 'Manrope' },
  { value: 'system', label: 'Системный' },
] as const
const sizes = ([90, 100, 110, 120] as const).map(value => ({ value, label: `${value}%` }))
const spacing = [
  { value: 1.45, label: 'Компактный' }, { value: 1.6, label: 'Обычный' }, { value: 1.8, label: 'Свободный' },
] as const
const headingFonts = [{ value: 'serif', label: 'С засечками' }, { value: 'body', label: 'Шрифт ответа' }] as const
const modes = [{ value: 'theme', label: 'По теме' }, { value: 'accent', label: 'Акцент темы' }, { value: 'custom', label: 'Свой цвет' }] as const

watch(() => props.mode, mode => { previewMode.value = mode })
watch(() => draft.value.font, font => {
  if (font !== 'inherit') loadFonts({ font, codeFont: 'system' })
}, { immediate: true })
watch(draft, () => { status.value = '' }, { deep: true })

function setColor(key: 'lightHeading' | 'darkHeading', event: Event) {
  const color = readHex((event.target as HTMLInputElement).value)
  if (color) draft.value[key] = color
}

function useSuggestion(key: 'lightHeading' | 'darkHeading') {
  const color = colors.value.find(item => item.key === key)?.suggestion
  if (color) draft.value[key] = color
}

function rememberField(event: FocusEvent) {
  if (event.target instanceof HTMLElement) lastField = event.target
}

function save() {
  if (!changed.value || !valid.value) return
  emit('save', { ...draft.value })
  status.value = 'Изменения применены в макете.'
}

function cancel() {
  draft.value = { ...props.saved }
}

async function goToPreview() {
  await nextTick()
  preview.value?.focus({ preventScroll: true })
  preview.value?.scrollIntoView({ block: 'start' })
}

function returnToEditor() {
  const target = lastField?.isConnected ? lastField : previewShortcut.value
  target?.focus()
}
</script>

<template>
  <section class="studio-appearance studio-scroll" aria-labelledby="studio-appearance-title">
    <div class="studio-settings-inner">
      <header class="studio-settings-title">
        <h1 id="studio-appearance-title">Оформление</h1>
        <p>Настройте, как выглядят ответы Budz.</p>
        <button ref="previewShortcut" class="studio-preview-shortcut" type="button" @click="goToPreview">
          <Eye aria-hidden="true" />К предпросмотру
        </button>
      </header>

      <div class="studio-settings-grid">
        <form id="studio-reading-form" class="studio-settings-form" @submit.prevent="save" @focusin="rememberField">
          <section class="studio-settings-card" aria-labelledby="studio-text-title">
            <div class="studio-section-title"><Type aria-hidden="true" /><h2 id="studio-text-title">Текст ответов</h2></div>
            <div class="studio-fields">
              <UiSelect v-model="draft.font" label="Шрифт ответов" :options="fonts" />
              <UiSelect v-model="draft.scale" label="Размер текста" :options="sizes" />
              <UiSelect v-model="draft.lineHeight" label="Межстрочный интервал" :options="spacing" />
              <UiSelect v-model="draft.headings" label="Начертание заголовков" :options="headingFonts" />
            </div>
            <fieldset class="studio-color-mode">
              <legend>Цвет заголовков ответа</legend>
              <label v-for="mode in modes" :key="mode.value">
                <input v-model="draft.headingColor" type="radio" name="studio-heading-color" :value="mode.value">
                <span>{{ mode.label }}</span>
              </label>
            </fieldset>
            <div v-if="draft.headingColor === 'custom'" class="studio-color-grid">
              <div v-for="color in colors" :key="color.mode" class="studio-color-field">
                <label>
                  <span>{{ color.label }}</span>
                  <span class="studio-color-value">
                    <input type="color" :value="draft[color.key]" :aria-label="`Цвет заголовков · ${color.label.toLowerCase()}`"
                      @input="setColor(color.key, $event)">
                    <span>{{ draft[color.key].toUpperCase() }}</span>
                  </span>
                </label>
                <small v-if="color.ratio >= 4.5">Контраст {{ color.ratio.toFixed(1) }}:1</small>
                <div v-else class="studio-color-warning" role="status">
                  <span>Недостаточный контраст</span>
                  <button v-if="color.suggestion" type="button" @click="useSuggestion(color.key)">
                    Выбрать читаемый оттенок
                  </button>
                </div>
              </div>
            </div>
          </section>
        </form>

        <aside ref="preview" class="studio-settings-preview" tabindex="-1" aria-labelledby="studio-preview-title">
          <header>
            <h2 id="studio-preview-title">Предпросмотр</h2>
            <div class="studio-preview-modes" role="group" aria-label="Тема предпросмотра">
              <button type="button" aria-label="Светлый предпросмотр" :aria-pressed="previewMode === 'light'" @click="previewMode = 'light'"><Sun aria-hidden="true" /></button>
              <button type="button" aria-label="Тёмный предпросмотр" :aria-pressed="previewMode === 'dark'" @click="previewMode = 'dark'"><Moon aria-hidden="true" /></button>
            </div>
          </header>
          <div class="studio-paper studio-preview-paper" :data-paper-mode="previewMode"
            :style="{ ...paperStyles(previewMode), ...readingStyles(draft, previewMode) }">
            <div class="studio-assistant-meta"><span class="studio-avatar-mark" aria-hidden="true">b.</span><strong>Budz</strong><span class="studio-preview-dot" role="img" aria-label="Сохранённая закладка"></span></div>
            <ChatMarkdown class="studio-response" :content="previewAnswer" />
          </div>
          <button class="studio-preview-shortcut" type="button" @click="returnToEditor">Вернуться к настройкам</button>
        </aside>
      </div>
      <div v-if="changed" class="studio-settings-actions">
        <span>Несохранённые изменения</span>
        <div class="studio-settings-buttons">
          <UiButton @click="cancel">Отменить</UiButton>
          <UiButton variant="primary" type="submit" form="studio-reading-form" :disabled="!valid">Сохранить</UiButton>
        </div>
      </div>
      <p v-if="status" class="studio-saved-status" role="status">{{ status }}</p>
    </div>
  </section>
</template>
