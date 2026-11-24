<script setup lang="ts">
import { nextTick, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { X } from 'lucide-vue-next'
import { api, explain } from '../api'
import type { MessageLink, MessageLinks } from '../types'
import UiIconButton from './ui/UiIconButton.vue'

type Kind = 'questions' | 'bookmarks' | 'search'
type Listing = MessageLinks & { loading: boolean; error: string }
const props = defineProps<{ chatId: string; temporary: boolean }>()
const emit = defineEmits<{ jump: [id: string] }>()
const dialog = ref<HTMLDialogElement>()
const searchInput = ref<HTMLInputElement>()
const mode = ref<'outline' | 'search'>('outline')
const term = ref('')
const activeIndex = ref(-1)
const empty = (): Listing => ({ items: [], next: null, loading: false, error: '' })
const results = reactive<Record<Kind, Listing>>({ questions: empty(), bookmarks: empty(), search: empty() })
const requests = new Map<Kind, AbortController>()

async function load(kind: Kind, more = false) {
  requests.get(kind)?.abort()
  const cursor = more ? results[kind].next : null
  if (!more) results[kind] = empty()
  if (!props.chatId || (kind === 'search' && !term.value.trim()) || (kind === 'bookmarks' && props.temporary)) return
  const request = new AbortController()
  requests.set(kind, request)
  const query = new URLSearchParams({ kind })
  if (kind === 'search') query.set('search', term.value.trim())
  if (cursor) query.set('after', cursor)
  results[kind].loading = true
  results[kind].error = ''
  try {
    const response = await api<MessageLinks>(`/chats/${props.chatId}/navigation?${query}`, { signal: request.signal })
    if (request.signal.aborted) return
    results[kind].items = more ? [...results[kind].items, ...response.items] : response.items
    results[kind].next = response.next
  } catch (reason) {
    if (!request.signal.aborted) results[kind].error = explain(reason)
  } finally {
    if (!request.signal.aborted) results[kind].loading = false
    if (requests.get(kind) === request) requests.delete(kind)
  }
}

async function show(kind: typeof mode.value, refresh = false) {
  mode.value = kind
  if (!dialog.value?.open) dialog.value?.showModal()
  await nextTick()
  if (kind === 'search') {
    searchInput.value?.focus()
    if ((refresh || !results.search.items.length) && term.value.trim()) void load('search')
  } else {
    void load('questions')
    void load('bookmarks')
  }
}

function choose(item: MessageLink, index?: number) {
  if (index !== undefined) activeIndex.value = index
  dialog.value?.close()
  emit('jump', item.id)
}

async function step(change: -1 | 1) {
  const next = activeIndex.value + change
  if (next >= results.search.items.length && results.search.next) await load('search', true)
  const item = results.search.items[next]
  if (item) choose(item, next)
}

watch(term, (_, __, onCleanup) => {
  requests.get('search')?.abort()
  activeIndex.value = -1
  results.search = empty()
  if (!term.value.trim()) return
  results.search.loading = true
  const timer = window.setTimeout(() => void load('search'), 230)
  onCleanup(() => window.clearTimeout(timer))
})
watch(() => props.chatId, () => {
  dialog.value?.close()
  requests.forEach(request => request.abort())
  term.value = ''
  activeIndex.value = -1
  results.questions = empty(); results.bookmarks = empty(); results.search = empty()
})
onBeforeUnmount(() => requests.forEach(request => request.abort()))
defineExpose({ show })
</script>

<template>
  <dialog ref="dialog" class="navigation-panel" aria-labelledby="chat-navigation-title" @click.self="dialog?.close()">
    <div class="navigation-content">
      <header class="navigation-heading">
        <h2 id="chat-navigation-title">{{ mode === 'search' ? 'Поиск по беседе' : 'Оглавление' }}</h2>
        <UiIconButton :icon="X" label="Закрыть панель" autofocus @click="dialog?.close()" />
      </header>
      <template v-if="mode === 'outline'">
        <section v-for="section in (['questions', 'bookmarks'] as const)" v-show="section !== 'bookmarks' || !temporary" :key="section" class="navigation-list">
          <h3>{{ section === 'questions' ? 'Вопросы' : 'Закладки' }}</h3>
          <p v-if="results[section].error" class="notice danger" role="alert">{{ results[section].error }}
            <button type="button" class="text-button" @click="load(section)">Повторить</button>
          </p>
          <p v-else-if="!results[section].loading && !results[section].items.length" class="muted">
            {{ section === 'questions' ? 'В этой беседе пока нет вопросов.' : 'Отметьте полезный ответ, чтобы вернуться к нему.' }}
          </p>
          <button v-for="item in results[section].items" :key="item.id" class="navigation-item" type="button" @click="choose(item)">
            <span v-if="section === 'bookmarks'" class="saved-marker" aria-hidden="true"></span>
            <span class="navigation-snippet">{{ item.text.trim() || 'Сообщение с вложением' }}</span>
          </button>
          <p v-if="results[section].loading" class="muted" role="status">Загружаем…</p>
          <button v-if="results[section].next" type="button" class="secondary" :disabled="results[section].loading" @click="load(section, true)">Показать ещё</button>
        </section>
      </template>
      <template v-else>
        <label for="chat-message-query">Слово или фраза</label>
        <input id="chat-message-query" ref="searchInput" v-model="term" type="search" maxlength="160" autocomplete="off">
        <p v-if="!term.trim()" class="muted">Введите текст, который хотите найти.</p>
        <p v-else-if="results.search.error" class="notice danger" role="alert">{{ results.search.error }}
          <button type="button" class="text-button" @click="load('search')">Повторить</button>
        </p>
        <p v-else-if="!results.search.loading && !results.search.items.length" class="muted" role="status">Совпадений нет.</p>
        <div v-if="results.search.items.length" class="actions">
          <button type="button" class="secondary" :disabled="activeIndex <= 0 || results.search.loading" @click="step(-1)">Предыдущее</button>
          <button type="button" class="secondary" :disabled="results.search.loading || (activeIndex + 1 >= results.search.items.length && !results.search.next)"
            @click="step(1)">Следующее</button>
        </div>
        <div class="navigation-list">
          <button v-for="(item, index) in results.search.items" :key="item.id" type="button" class="navigation-item search-result"
            :aria-current="index === activeIndex ? 'true' : undefined" @click="choose(item, index)">
            <small>{{ item.role === 'assistant' ? 'Budz' : 'Вы' }}</small>
            <span>{{ item.text.slice(0, item.matchStart) }}<mark>{{ item.text.slice(item.matchStart, item.matchStart + item.matchLength) }}</mark>{{ item.text.slice(item.matchStart + item.matchLength) }}</span>
          </button>
          <p v-if="results.search.loading" class="muted" role="status">Ищем совпадения…</p>
          <button v-if="results.search.next" type="button" class="secondary" :disabled="results.search.loading" @click="load('search', true)">Показать ещё</button>
        </div>
      </template>
    </div>
  </dialog>
</template>

<style scoped>
.navigation-panel { inset: var(--space-3) var(--space-3) var(--space-3) auto; margin: 0; width: min(380px, calc(100% - 24px));
  max-width: none; max-height: calc(100dvh - 2 * var(--space-3)); padding: 0; overflow: auto;
  overscroll-behavior: contain; border-radius: var(--radius-lg); }
.navigation-content { display: grid; align-content: start; gap: var(--space-3); padding: var(--space-4); }
.navigation-heading { display: flex; align-items: center; justify-content: space-between; gap: var(--space-2); }
.navigation-heading h2 { font-family: var(--font-serif); font-weight: 500; }
.navigation-list { display: grid; gap: var(--space-2); min-width: 0; }
.navigation-item { display: flex; align-items: center; gap: var(--space-2); min-height: 42px; padding: var(--space-2) var(--space-3);
  border: 1px solid var(--border); border-radius: var(--radius-md); background: var(--surface-1); color: var(--text);
  overflow-wrap: anywhere; text-align: start; }
.navigation-item:is(:hover, [aria-current]) { background: var(--surface-3); }
.navigation-snippet { display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 3; overflow: hidden; }
.search-result { display: grid; }
.search-result small { color: var(--text-muted); }
.search-result mark { background: var(--surface-3); color: var(--text); font-weight: 750; }
.saved-marker { flex: 0 0 8px; height: 8px; border-radius: 50%; background: var(--studio-bookmark); }
@media (max-width: 900px) {
  .navigation-panel { inset: auto 0 0; width: 100%; max-height: 82dvh; border-radius: var(--radius-lg) var(--radius-lg) 0 0; }
  .navigation-content { padding-block-end: max(var(--space-4), env(safe-area-inset-bottom, 0px)); }
  .navigation-item, .navigation-content :deep(button) { min-height: 44px; min-width: 44px; }
}
</style>
