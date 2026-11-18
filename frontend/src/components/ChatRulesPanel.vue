<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api, explain } from '../api'
import { expertiseOptions, formatOptions, labelFor, languageOptions, lengthOptions, taskOptions, toneOptions } from '../profile-options'
import type { ChatRules } from '../types'

const props = defineProps<{ chatId: string; workspaceId: string; last?: ChatRules | null }>()
const current = ref<ChatRules>()
const applied = ref<ChatRules>()
const tab = ref<'current' | 'applied'>('current')
const error = ref('')
const busy = ref(false)
const dialog = ref<HTMLDialogElement>()
const display = computed(() => tab.value === 'applied' ? applied.value : !busy.value && !error.value ? current.value : undefined)
let loadVersion = 0

watch(() => [props.chatId, props.workspaceId], () => {
  if (dialog.value?.open) dialog.value.close()
  loadVersion++
  current.value = undefined
  applied.value = undefined
})

async function load() {
  const version = ++loadVersion
  busy.value = true
  error.value = ''
  try {
    const path = props.chatId ? `/chats/${props.chatId}/rules` :
      `/chats/rules${props.workspaceId ? `?workspaceId=${encodeURIComponent(props.workspaceId)}` : ''}`
    const value = await api<ChatRules>(path)
    if (version === loadVersion) current.value = value
  } catch (reason) { if (version === loadVersion) error.value = explain(reason) }
  finally { if (version === loadVersion) busy.value = false }
}

function show(rules?: ChatRules | null) {
  applied.value = rules ?? props.last ?? undefined
  tab.value = rules ? 'applied' : 'current'
  dialog.value?.showModal()
  void load()
}
defineExpose({ show })
</script>

<template>
  <dialog ref="dialog" class="rules-dialog" aria-labelledby="rules-title">
    <section class="stack">
      <h2 id="rules-title">Правила диалога</h2>
      <div v-if="applied" class="actions" role="group" aria-label="Какие правила показать">
        <button type="button" class="secondary" :aria-pressed="tab === 'current'" @click="tab = 'current'">Текущие настройки</button>
        <button type="button" class="secondary" :aria-pressed="tab === 'applied'" @click="tab = 'applied'">Для ответа</button>
      </div>
      <p v-if="tab === 'current' && busy" class="muted" role="status">Загружаем правила…</p>
      <p v-if="tab === 'current' && error" class="notice danger" role="alert">{{ error }} <button class="text-button" @click="load">Повторить</button></p>
      <template v-if="display">
          <p class="help">{{ tab === 'current' ? 'Настройки для следующего запроса. Тип задачи определяется по его тексту.' : 'Настройки, использованные для этого ответа.' }}</p>
          <dl class="context-list">
            <div v-if="display.workspaceName"><dt>Пространство</dt><dd>{{ display.workspaceName }}</dd></div>
            <div v-if="display.task"><dt>Тип задачи</dt><dd>{{ taskOptions[display.task] }}</dd></div>
            <div v-else><dt>Тип задачи</dt><dd>{{ display.adaptToTask ? 'Определяется по запросу' : 'Общие правила' }}</dd></div>
            <div><dt>Язык</dt><dd>{{ labelFor(languageOptions, display.language) }}</dd></div>
            <div><dt>Тон и уровень</dt><dd>{{ labelFor(toneOptions, display.tone) }} · {{ labelFor(expertiseOptions, display.level) }}</dd></div>
            <div><dt>Длина и формат</dt><dd>{{ labelFor(lengthOptions, display.length) }} · {{ labelFor(formatOptions, display.format) }}</dd></div>
            <div v-if="display.persona"><dt>Роль</dt><dd>{{ display.persona }}</dd></div>
            <div><dt>Память аккаунта</dt><dd>{{ display.memoryUsed ? 'Учтена' : display.memoryEnabled ? tab === 'current' ? 'Разрешена' : 'Факты не вошли' : 'Выключена' }}</dd></div>
            <div v-if="display.workspaceName"><dt>Материалы пространства</dt><dd>{{ display.workspaceMaterials ? tab === 'current' ? 'Доступны' : 'Учтены' : 'Нет в контексте' }}</dd></div>
          </dl>
          <section v-if="display.workspaceInstructions" class="rule-note"><h3>Инструкции пространства</h3><p>{{ display.workspaceInstructions }}</p></section>
          <section v-if="display.taskRule || display.taskInstructions" class="rule-note"><h3>Правила задачи</h3><p v-if="display.taskRule">{{ display.taskRule }}</p><p v-if="display.taskInstructions">{{ display.taskInstructions }}</p></section>
          <section v-if="display.goal" class="rule-note"><h3>Цель из профиля</h3><p>{{ display.goal }}</p></section>
      </template>
      <div class="actions"><button class="secondary" type="button" @click="dialog?.close()">Закрыть</button></div>
    </section>
  </dialog>
</template>

<style scoped>
.rules-dialog { width: min(92vw, 570px); }
.rule-note { display: grid; gap: .4rem; }
.rule-note p { white-space: pre-wrap; }
button[aria-pressed="true"] { background: var(--pale); border-color: var(--accent); }
</style>
