<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { Check } from 'lucide-vue-next'
import { explain } from '../api'
import { useSession } from '../session'
import type { InterfaceVariant } from '../types'
import UiButton from './ui/UiButton.vue'
import UiSegmented from './ui/UiSegmented.vue'
import { useToast } from './ui/use-toast'

const options = [
  { value: 'current', label: 'Текущий Budz', description: 'Знакомая навигация и панели чата.' },
  { value: 'focus', label: 'Фокус', description: 'Компактная шапка и просторное поле ввода.' },
] as const
const previewOptions = [{ value: 'start', label: 'Первый экран' }, { value: 'chat', label: 'Открытый диалог' }] as const
const session = useSession()
const toast = useToast()
const id = useId()
const variant = ref<InterfaceVariant>(session.savedInterfaceVariant)
const preview = ref<'start' | 'chat'>('chat')
const error = ref('')
const canApply = computed(() => variant.value !== session.savedInterfaceVariant || variant.value !== session.interfaceVariant)
const activeLabel = computed(() => options.find(option => option.value === session.interfaceVariant)?.label)

watch(() => session.savedInterfaceVariant, (next, previous) => {
  if (variant.value === previous) variant.value = next
})
watch(variant, () => { error.value = '' })

async function apply() {
  if (!canApply.value || session.interfaceSaving) return
  error.value = ''
  try {
    await session.setInterfaceVariant(variant.value)
    toast.success('Интерфейс сохранён.')
  } catch (reason) { error.value = explain(reason) }
}
</script>

<template>
  <section class="interface-settings stack" :aria-labelledby="`${id}-heading`">
    <div class="interface-heading">
      <h2 :id="`${id}-heading`">Интерфейс</h2>
      <UiSegmented v-model="preview" label="Экран предпросмотра" :options="previewOptions" hide-label size="sm" />
    </div>
    <form class="stack" @submit.prevent="apply">
      <fieldset class="interfaces" :disabled="session.interfaceSaving">
        <legend class="sr-only">Вариант интерфейса</legend>
        <label v-for="option in options" :key="option.value" class="interface-choice">
          <span class="interface-preview" :data-variant="option.value" :data-preview="preview" aria-hidden="true">
            <span class="mini-header"><strong>budz<span>.</span></strong><i v-if="option.value === 'current'"></i><b></b></span>
            <span class="mini-history"><i></i><i></i><i></i></span>
            <span class="mini-content">
              <strong>{{ preview === 'chat' ? 'План подготовки' : option.value === 'focus' ? 'С чего начнём?' : 'Что хотите разобрать?' }}</strong>
              <template v-if="preview === 'chat'">
                <span class="mini-user">Помоги составить план</span>
                <span class="mini-answer"><span>Начнём с двух тем.</span><i></i></span>
              </template>
              <span class="mini-composer"><span>Напишите вопрос</span><b>↑</b></span>
            </span>
          </span>
          <span class="choice-label">
            <input v-model="variant" type="radio" :name="`${id}-interface`" :value="option.value"
              :aria-label="option.label" :aria-describedby="`${id}-${option.value}`">
            <strong>{{ option.label }}</strong>
            <span v-if="variant === option.value" class="chosen" aria-hidden="true"><Check /> Выбрано</span>
          </span>
          <span :id="`${id}-${option.value}`" class="muted choice-description">{{ option.description }}</span>
        </label>
      </fieldset>
      <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
      <div class="actions">
        <UiButton type="submit" variant="primary" :disabled="!canApply || session.interfaceSaving" :aria-busy="session.interfaceSaving">
          {{ session.interfaceSaving ? 'Сохраняем…' : error ? 'Повторить сохранение интерфейса' : 'Применить интерфейс' }}
        </UiButton>
        <p class="muted" role="status">Сейчас действует «{{ activeLabel }}»</p>
      </div>
    </form>
  </section>
</template>

<style scoped>
.interface-settings { margin-block-end: var(--space-6); padding-block: var(--space-3) var(--space-5); border-block-end: 1px solid var(--border); }
.interface-heading { display: flex; gap: var(--space-3); align-items: center; justify-content: space-between; flex-wrap: wrap; }
.interface-heading h2 { font-size: var(--font-size-xl); }
.interfaces { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-3); padding: 0; border: 0; min-width: 0; }
.interface-choice { display: grid; gap: var(--space-1); min-width: 0; padding: var(--space-3); background: var(--surface-1);
  border: 1px solid var(--border); border-radius: var(--radius-md); font-weight: 400; cursor: pointer; }
.interface-choice:has(input:checked) { border-color: var(--accent); box-shadow: inset 0 0 0 1px var(--accent); }
.interface-choice:has(input:focus-visible) { outline: 2px solid var(--focus); outline-offset: 2px; }
.choice-label { display: flex; gap: var(--space-2); min-height: 37px; align-items: center; }
.choice-label input { flex: none; width: 16px; height: 16px; min-height: 0; padding: 0; margin: 0; accent-color: var(--accent); }
.choice-description { font-size: var(--font-size-sm); }
.chosen { display: inline-flex; align-items: center; gap: 3px; margin-inline-start: auto; font-size: var(--font-size-xs); color: var(--accent); }
.chosen svg { width: 14px; height: 14px; }
.interface-preview { display: grid; grid-template: 26px minmax(0, 1fr) / 24% minmax(0, 1fr); min-height: 144px;
  overflow: hidden; border: 1px solid var(--border); border-radius: var(--radius-sm); background: var(--surface-0); font-size: var(--font-size-xs); }
.mini-header { display: flex; align-items: center; gap: var(--space-1); grid-column: 1 / -1; padding-inline: var(--space-2);
  background: var(--surface-1); border-block-end: 1px solid var(--border); }
.mini-header strong { font: 700 1.1em var(--font-serif); letter-spacing: -.06em; }
.mini-header strong span, .mini-answer { color: var(--accent); }
.mini-header i { width: 23%; height: 4px; border-radius: var(--radius-full); background: var(--border); margin-inline: auto; }
.mini-header b { width: 9px; height: 9px; margin-inline-start: auto; border-radius: var(--radius-full); background: var(--accent-2); }
.mini-history { display: grid; align-content: start; gap: 6px; border-inline-end: 1px solid var(--border); background: var(--surface-3); padding: var(--space-2); }
.mini-history i { width: 100%; height: 4px; background: var(--border); border-radius: var(--radius-full); }
.mini-history i:first-child { height: 11px; border: 1px solid var(--border); background: var(--surface-1); border-radius: 4px; }
.mini-history i:last-child { width: 67%; }
.mini-content { display: grid; gap: var(--space-1); align-content: center; min-width: 0; padding: var(--space-2) clamp(6px, 2vw, 20px); }
.mini-content > strong { overflow-wrap: anywhere; }
.mini-user { justify-self: end; max-width: 100%; padding-inline: 4px; background: var(--surface-2); border-radius: 5px; }
.mini-answer { display: grid; gap: 3px; padding-inline: 4px; border: 1px solid var(--border); }
.mini-answer i { width: 62%; height: 3px; border-radius: var(--radius-full); background: var(--border); }
.interface-preview[data-variant="focus"] .mini-answer { border-color: transparent; padding-inline: 0; }
.mini-composer { display: flex; justify-content: space-between; align-items: center; gap: 3px; min-height: 16px; padding: 2px 5px;
  border: 1px solid var(--border); background: var(--surface-1); border-radius: 4px; color: var(--text-muted); }
.mini-composer b { color: var(--btn-primary-text); background: var(--btn-primary-bg); border-radius: 4px; padding-inline: 3px; }
.interface-preview[data-variant="focus"] .mini-composer { min-height: 23px; border-radius: 9px; }
@media (max-width: 620px) { .interfaces { grid-template-columns: minmax(0, 1fr); } }
</style>
