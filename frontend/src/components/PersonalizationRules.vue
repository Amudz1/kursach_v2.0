<script setup lang="ts">
import { computed, ref } from 'vue'
import { BookOpen, Code, Lightbulb, ListChecks, PenLine } from 'lucide-vue-next'
import { formatOptions, labelFor, lengthOptions, taskOptions } from '../profile-options'
import type { Profile, TaskMode, TaskPreference } from '../types'

const profile = defineModel<Profile>({ required: true })
const selected = ref<TaskMode>('code')
const rule = computed(() => profile.value.taskRules.find(({ task }) => task === selected.value))
const icons = { general: Lightbulb, code: Code, learn: BookOpen, write: PenLine, plan: ListChecks }
const colors = { general: 'cat-1', code: 'cat-2', learn: 'cat-3', write: 'cat-4', plan: 'cat-5' }

const descriptions: Record<TaskMode, string> = {
  general: 'Вопросы, для которых не найден отдельный тип задачи.',
  code: 'Разбор функций, отладка и примеры программ.',
  learn: 'Объяснение тем, примеры и помощь в освоении материала.',
  write: 'Написание и редактура текстов с учётом аудитории.',
  plan: 'Планы, этапы и расстановка приоритетов.',
}

function resetRule() {
  profile.value.taskRules = profile.value.taskRules.filter(({ task }) => task !== selected.value)
}

function changeRule(key: 'length' | 'format' | 'instructions', value: string) {
  const current: TaskPreference = rule.value ?? { task: selected.value, length: null, format: null, instructions: null }
  const next = { ...current, [key]: value || null }
  const rules = profile.value.taskRules.filter(({ task }) => task !== selected.value)
  if (next.length || next.format || next.instructions) rules.push(next)
  profile.value.taskRules = rules
}
</script>

<template>
  <div class="stack">
    <label class="check mode-switch"><input v-model="profile.adaptToTask" type="checkbox"> Определять тип задачи по запросу</label>
    <template v-if="profile.adaptToTask">
      <div class="task-layout">
        <div class="task-list" role="group" aria-label="Тип задачи">
          <button v-for="(label, task) in taskOptions" :key="task" type="button" :aria-pressed="selected === task" @click="selected = task">
            <span class="task-caption"><component :is="icons[task]" :style="{ '--task-color': `var(--${colors[task]})` }" aria-hidden="true" />{{ label }}</span>
            <span v-if="profile.taskRules.some((item) => item.task === task)" class="custom-mark" title="Есть своё правило" aria-label="Есть своё правило"></span>
          </button>
        </div>
        <section class="task-editor stack" :aria-label="taskOptions[selected]">
          <div><h3>{{ taskOptions[selected] }}</h3><p class="help">{{ descriptions[selected] }}</p></div>
          <label>Длина для задачи
            <select :value="rule?.length ?? ''" aria-label="Длина для задачи" @change="changeRule('length', ($event.target as HTMLSelectElement).value)">
              <option value="">Из стиля · {{ labelFor(lengthOptions, profile.responseLength) }}</option>
              <option v-for="(label, code) in lengthOptions" :key="code" :value="code">{{ label }}</option>
            </select>
          </label>
          <label>Формат для задачи
            <select :value="rule?.format ?? ''" aria-label="Формат для задачи" @change="changeRule('format', ($event.target as HTMLSelectElement).value)">
              <option value="">Из стиля · {{ labelFor(formatOptions, profile.responseFormat) }}</option>
              <option v-for="(label, code) in formatOptions" :key="code" :value="code">{{ label }}</option>
            </select>
          </label>
          <label>Инструкция для задачи
            <textarea :value="rule?.instructions ?? ''" rows="3" maxlength="300" placeholder="Что Budz должен делать в таких ответах?"
              @input="changeRule('instructions', ($event.target as HTMLTextAreaElement).value)"></textarea>
          </label>
          <button v-if="rule" type="button" class="text-button self-start" @click="resetRule">Сбросить правило задачи</button>
        </section>
      </div>
    </template>
    <p v-else class="help">Ответы будут следовать общему стилю. Правила задач снова вступят в силу после включения адаптации.</p>
  </div>
</template>

<style scoped>
.task-caption { display: inline-flex; align-items: center; gap: var(--space-2); }
.task-caption svg { width: 16px; height: 16px; flex: 0 0 auto; color: light-dark(var(--task-color), color-mix(in oklab, var(--task-color), white 55%)); }
.mode-switch { min-height: 45px; padding: .5rem .75rem; background: var(--pale); border-radius: 8px; font-size: .89rem; }
.task-layout { display: grid; grid-template-columns: minmax(126px, .75fr) minmax(0, 1.65fr); align-items: start; gap: .8rem; }
.task-list { display: grid; gap: .2rem; }
.task-list button { display: flex; justify-content: space-between; align-items: center; gap: .3rem; min-height: 39px;
  padding: .48rem .6rem; text-align: left; font-size: .83rem; font-weight: 620; color: var(--ink);
  background: transparent; border: 1px solid transparent; border-radius: 7px; }
.task-list button:hover, .task-list button[aria-pressed=true] { background: var(--inset); }
.task-list button[aria-pressed=true] { border-color: var(--rule); }
.custom-mark { flex: 0 0 auto; width: .42rem; height: .42rem; border-radius: 50%; background: var(--accent); }
.task-editor { padding: .85rem; border: 1px solid var(--rule); border-radius: 9px; }
.task-editor h3 { margin-bottom: .25rem; }
@media (max-width: 560px) {
  .task-layout { grid-template-columns: 1fr; }
  .task-list { display: flex; flex-wrap: wrap; }
  .task-list button { padding-inline: .5rem; min-height: 35px; }
}
</style>
