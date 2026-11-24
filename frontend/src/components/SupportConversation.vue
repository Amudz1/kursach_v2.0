<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref } from 'vue'
import { ApiError, api, explain } from '../api'
import { supportTime, type SupportHistory, type SupportMessage } from '../help/support'
import UiButton from './ui/UiButton.vue'
import UiTextarea from './ui/UiTextarea.vue'

const props = defineProps<{ endpoint: string; staff?: boolean; username?: string }>()
const entries = ref<SupportMessage[]>([])
const log = ref<HTMLOListElement>()
const previous = ref<number | null>(null)
const draft = ref('')
const readError = ref('')
const sendError = ref('')
const loading = ref(true)
const loadingPrevious = ref(false)
const sending = ref(false)
const life = new AbortController()
let checkedThrough: number | undefined
let refreshing = false
let timer: number | undefined
let pending: { content: string; requestId: string } | undefined

function merge(messages: SupportMessage[]) {
  if (!messages.length) return
  const byId = new Map(entries.value.map(message => [message.id, message]))
  for (const message of messages) byId.set(message.id, message)
  entries.value = [...byId.values()].sort((first, second) => first.id - second.id)
}

async function latest() {
  if (refreshing || life.signal.aborted) return
  refreshing = true
  const first = checkedThrough === undefined
  const element = log.value
  const atEnd = !element || element.scrollHeight - element.scrollTop - element.clientHeight < 40
  try {
    const query = first ? '' : `?after=${checkedThrough}`
    const page = await api<SupportHistory>(props.endpoint + query, { signal: life.signal })
    if (first) previous.value = page.previous
    checkedThrough = page.messages.at(-1)?.id ?? checkedThrough ?? 0
    merge(page.messages)
    readError.value = ''
    await nextTick()
    if (atEnd && log.value) log.value.scrollTop = log.value.scrollHeight
  } catch (error) {
    if (!life.signal.aborted) readError.value = explain(error)
  } finally {
    refreshing = false
    loading.value = false
  }
}

async function older() {
  if (previous.value === null || loadingPrevious.value) return
  loadingPrevious.value = true
  try {
    const page = await api<SupportHistory>(`${props.endpoint}?before=${previous.value}`, { signal: life.signal })
    const element = log.value
    const height = element?.scrollHeight ?? 0
    merge(page.messages)
    previous.value = page.previous
    readError.value = ''
    await nextTick()
    if (element) element.scrollTop += element.scrollHeight - height
  } catch (error) {
    if (!life.signal.aborted) readError.value = explain(error)
  } finally { loadingPrevious.value = false }
}

async function send() {
  const content = draft.value.trim()
  if (!content || sending.value) return
  sending.value = true
  sendError.value = ''
  try {
    if (pending?.content !== content) pending = { content, requestId: crypto.randomUUID() }
    const message = await api<SupportMessage>(props.endpoint, { method: 'POST', body: pending, signal: life.signal })
    merge([message])
    draft.value = ''
    pending = undefined
    await nextTick()
    if (log.value) log.value.scrollTop = log.value.scrollHeight
    void latest()
  } catch (error) {
    if (error instanceof ApiError && error.code === 'support_request_conflict') pending = undefined
    if (!life.signal.aborted) sendError.value = explain(error)
  } finally { sending.value = false }
}

function refreshVisible() {
  if (!document.hidden) void latest()
}

onMounted(() => {
  void latest()
  timer = window.setInterval(refreshVisible, 5000)
  document.addEventListener('visibilitychange', refreshVisible)
})
onUnmounted(() => {
  life.abort()
  window.clearInterval(timer)
  document.removeEventListener('visibilitychange', refreshVisible)
})
</script>

<template>
  <div class="fieldset stack support-conversation">
    <p v-if="loading" class="muted" role="status">Загружаем переписку…</p>
    <p v-if="readError" class="notice danger" role="alert">{{ readError }} <button type="button" class="text-button" @click="latest">Обновить</button></p>
    <p v-if="!loading && !readError && !entries.length" class="muted">Сообщений пока нет.</p>
    <UiButton v-if="previous !== null" type="button" variant="secondary" size="sm" class="self-start" :loading="loadingPrevious" @click="older">
      Загрузить ранние сообщения
    </UiButton>
    <ol v-if="entries.length" ref="log" class="support-log" aria-label="История переписки" aria-live="polite" tabindex="0">
      <li v-for="message in entries" :key="message.id" :class="{ own: message.fromSupport === Boolean(staff) }">
        <div class="support-meta">
          <strong>{{ message.fromSupport ? 'Поддержка Budz' : staff ? username ?? 'Пользователь' : 'Вы' }}</strong>
          <time :datetime="message.createdAt">{{ supportTime.format(new Date(message.createdAt)) }}</time>
        </div>
        <p>{{ message.content }}</p>
      </li>
    </ol>
    <form class="stack" @submit.prevent="send">
      <UiTextarea v-model="draft" :label="staff ? 'Ответ пользователю' : 'Сообщение в поддержку'" :maxlength="4000" :rows="3" :disabled="sending" />
      <p v-if="sendError" class="notice danger" role="alert">{{ sendError }}</p>
      <UiButton type="submit" class="self-start" :disabled="!draft.trim()" :loading="sending">Отправить</UiButton>
    </form>
  </div>
</template>

<style scoped>
.support-conversation { min-width: 0; }
.support-log { display: grid; gap: var(--space-2); padding: var(--space-1); margin: 0; list-style: none;
  max-height: min(52dvh, 32rem); overflow: auto; overscroll-behavior: contain; overflow-anchor: none; }
.support-log li { justify-self: start; min-width: 0; max-width: min(100%, 36rem); padding: var(--space-2) var(--space-3);
  border: 1px solid var(--border); border-radius: var(--radius-md); background: var(--surface-1); }
.support-log li.own { justify-self: end; background: var(--surface-2); }
.support-log p { white-space: pre-wrap; overflow-wrap: anywhere; }
.support-meta { display: flex; flex-wrap: wrap; gap: var(--space-1) var(--space-2); margin-bottom: var(--space-1);
  color: var(--text-muted); font-size: var(--font-size-sm); overflow-wrap: anywhere; }
</style>
