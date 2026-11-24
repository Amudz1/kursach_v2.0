<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { BookOpen, ChevronRight, Ellipsis, Folder, Moon, PanelLeft, Send, Sun, X } from 'lucide-vue-next'
import { loadFonts } from '../../src/appearance/fonts'
import ChatMarkdown from '../../src/components/ChatMarkdown.vue'
import UiButton from '../../src/components/ui/UiButton.vue'
import UiToast from '../../src/components/ui/UiToast.vue'
import StudioAppearance from './StudioAppearance.vue'
import StudioHistory from './StudioHistory.vue'
import { defaultReadingStyle, paperStyles, readingStyles, type Mode } from './reading-style'
import { examples, starterPrompts, type ExampleThread, type Turn } from './sample-data'

type Screen = 'start' | 'chat' | 'appearance'
const parameters = new URLSearchParams(location.search)
const screenValue = parameters.get('screen')
const screen = ref<Screen>(screenValue === 'chat' || screenValue === 'appearance' ? screenValue : 'start')
const mode = ref<Mode>(parameters.get('mode') === 'dark' ? 'dark' : 'light')
const threads = ref<ExampleThread[]>(parameters.get('history') === 'empty'
  ? []
  : examples.map(thread => ({ ...thread, turns: [...thread.turns] })))
const threadId = ref(threads.value[0]?.id)
const thread = computed(() => threads.value.find(item => item.id === threadId.value))
const draft = ref('')
const input = ref<HTMLTextAreaElement>()
const scrollRegion = ref<HTMLElement>()
const historyPanel = ref<HTMLDialogElement>()
const outlinePanel = ref<HTMLDialogElement>()
const rulesPanel = ref<HTMLDialogElement>()
const chatMenu = ref<HTMLDetailsElement>()
const savedStyle = ref({ ...defaultReadingStyle })
const initialDraft = parameters.get('draft') === '1'
const bookmarks = ref(new Set(parameters.get('history') === 'empty' ? [] : ['defense:plan']))
const recent = computed(() => threads.value.find(item => item.id === 'defense') ?? threads.value.at(-1))
const recentTurn = computed(() => recent.value?.turns.find(turn => turn.id === 'plan') ?? recent.value?.turns.at(-1))
const currentId = computed(() => screen.value === 'chat' ? thread.value?.id : undefined)
const bookmarkKey = (id: string) => `${threadId.value}:${id}`
const isBookmarked = (id: string) => bookmarks.value.has(bookmarkKey(id))
const markedTurns = computed(() => thread.value?.turns.filter(turn => isBookmarked(turn.id)) ?? [])
const questions = computed(() => thread.value?.turns.filter(turn => turn.role === 'user') ?? [])
let nextId = 0

if (screen.value === 'chat' && !threadId.value) screen.value = 'start'
loadFonts({ font: 'plex', codeFont: 'system' })
watch(mode, value => {
  const root = document.documentElement
  root.dataset.studioMode = value
  root.dataset.theme = value
  for (const [key, color] of Object.entries(paperStyles(value))) root.style.setProperty(key, color)
}, { immediate: true })
watch([screen, mode], ([page, theme]) => {
  const url = new URL(location.href)
  url.searchParams.set('screen', page)
  url.searchParams.set('mode', theme)
  history.replaceState({}, '', url)
})

function navigate(page: Screen) {
  historyPanel.value?.close()
  screen.value = page
}

async function goToTurn(id: string, focus = true) {
  outlinePanel.value?.close()
  await nextTick()
  const element = document.getElementById(`studio-turn-${id}`)
  if (focus) element?.focus({ preventScroll: true })
  element?.scrollIntoView({ block: 'start' })
}

async function openThread(id: string, turnId?: string) {
  threadId.value = id
  navigate('chat')
  await nextTick()
  if (turnId) await goToTurn(turnId)
  else scrollRegion.value?.scrollTo({ top: 0 })
}

function toggleBookmark(turn: Turn) {
  const key = bookmarkKey(turn.id)
  if (bookmarks.value.has(key)) bookmarks.value.delete(key)
  else bookmarks.value.add(key)
}

function fill(text: string) {
  draft.value = text
  input.value?.focus()
}

async function send() {
  const body = draft.value.trim()
  if (!body) return
  const id = ++nextId
  const turns: Turn[] = [
    { id: `sample-question-${id}`, role: 'user', body },
    { id: `sample-answer-${id}`, role: 'assistant', label: 'Пример ответа',
      body: 'В макете используется пример ответа. Здесь появится ответ на ваш вопрос.' },
  ]
  draft.value = ''
  if (screen.value === 'chat' && thread.value) {
    thread.value.turns.push(...turns)
    await goToTurn(turns[0].id, false)
  } else {
    const idOfThread = `sample-${id}`
    threads.value.unshift({ id: idOfThread, title: body.slice(0, 60), group: 'Сегодня', turns })
    await openThread(idOfThread)
  }
}

function showRules() {
  if (chatMenu.value) chatMenu.value.open = false
  rulesPanel.value?.showModal()
}

onMounted(() => {
  const target = parameters.get('at')
  if (screen.value === 'chat' && target) void goToTurn(target, false)
})
</script>

<template>
  <a class="skip-link" href="#studio-main">Перейти к содержимому</a>
  <div class="studio-shell">
    <aside class="studio-side" aria-label="Навигация Budz">
      <StudioHistory :threads="threads" :active-id="currentId" :appearance-open="screen === 'appearance'"
        @start="navigate('start')" @select="openThread" @appearance="navigate('appearance')" />
    </aside>

    <main id="studio-main" class="studio-main">
      <header class="studio-bar">
        <div class="studio-bar-lead">
          <button class="studio-icon studio-history-trigger" type="button" aria-label="Открыть историю"
            aria-haspopup="dialog" aria-controls="studio-history-panel" @click="historyPanel?.showModal()"><PanelLeft aria-hidden="true" /></button>
          <div class="studio-location">
            <button type="button" @click="navigate('start')">Диалоги</button>
            <template v-if="screen === 'chat' && thread">
              <ChevronRight class="studio-crumb-icon" aria-hidden="true" />
              <span class="studio-crumb">{{ thread.title }}</span>
            </template>
            <template v-else-if="screen === 'appearance'">
              <ChevronRight class="studio-crumb-icon" aria-hidden="true" /><span class="studio-crumb">Оформление</span>
            </template>
          </div>
        </div>
        <div class="studio-bar-actions">
          <button v-if="screen === 'chat'" class="studio-icon studio-outline-trigger" type="button" aria-label="Оглавление"
            aria-haspopup="dialog" aria-controls="studio-outline-panel" @click="outlinePanel?.showModal()"><BookOpen aria-hidden="true" /><span>Оглавление</span></button>
          <details v-if="screen === 'chat'" ref="chatMenu" class="studio-more">
            <summary class="studio-icon" aria-label="Меню диалога"><Ellipsis aria-hidden="true" /></summary>
            <button type="button" @click="showRules">Правила диалога</button>
          </details>
          <button class="studio-icon studio-theme" type="button" :aria-label="mode === 'light' ? 'Включить тёмную тему' : 'Включить светлую тему'"
            @click="mode = mode === 'light' ? 'dark' : 'light'">
            <Moon v-if="mode === 'light'" aria-hidden="true" /><Sun v-else aria-hidden="true" />
            <span>{{ mode === 'light' ? 'Ночь' : 'День' }}</span>
          </button>
        </div>
      </header>

      <section v-if="screen === 'start'" class="studio-start studio-scroll" aria-labelledby="studio-start-title">
        <div class="studio-start-inner">
          <h1 id="studio-start-title">Что хотите<br>разобрать<span>?</span></h1>
          <form class="studio-composer studio-start-composer" @submit.prevent="send">
            <label class="sr-only" for="studio-first-text">Новый запрос</label>
            <textarea id="studio-first-text" ref="input" v-model="draft" rows="3" placeholder="Напишите вопрос или мысль…"></textarea>
            <div class="studio-composer-bottom">
              <UiButton variant="primary" type="submit" :icon="Send" :disabled="!draft.trim()">Отправить</UiButton>
            </div>
          </form>

          <button v-if="recent" type="button" class="studio-resume" @click="openThread(recent.id, recentTurn?.id)">
            <span class="studio-resume-dot" aria-hidden="true"></span>
            <span class="studio-resume-copy">
              <span class="studio-resume-label">Продолжить</span>
              <strong>{{ recent.title }}</strong>
              <span class="studio-resume-fragment">{{ [recent.project, recentTurn?.label].filter(Boolean).join(' · ') || recentTurn?.body.slice(0, 84) }}</span>
            </span>
            <BookOpen aria-hidden="true" class="studio-resume-icon" />
          </button>
          <ul v-else class="studio-prompt-list" aria-label="Примеры запросов">
            <li v-for="prompt in starterPrompts" :key="prompt"><button type="button" @click="fill(prompt)">{{ prompt }}</button></li>
          </ul>
        </div>
      </section>

      <section v-else-if="screen === 'chat' && thread" class="studio-chat" aria-labelledby="studio-chat-title">
        <div ref="scrollRegion" class="studio-transcript studio-scroll">
          <div class="studio-reading-column" :style="readingStyles(savedStyle, mode)">
            <header class="studio-conversation-heading">
              <p v-if="thread.project" class="studio-project-label"><Folder aria-hidden="true" />{{ thread.project }}</p>
              <h1 id="studio-chat-title">{{ thread.title }}</h1>
            </header>
            <div class="studio-turns">
              <article v-for="turn in thread.turns" :id="`studio-turn-${turn.id}`" :key="turn.id"
                class="studio-turn" :class="{ 'studio-user-turn': turn.role === 'user' }" tabindex="-1"
                :aria-label="turn.label || (turn.role === 'user' ? 'Сообщение пользователя' : 'Ответ Budz')">
                <template v-if="turn.role === 'assistant'">
                  <div class="studio-turn-top">
                    <div class="studio-assistant-meta"><span class="studio-avatar-mark" aria-hidden="true">b.</span><strong>Budz</strong></div>
                    <button class="studio-bookmark studio-icon" type="button" :aria-pressed="isBookmarked(turn.id)"
                      :aria-label="isBookmarked(turn.id) ? 'Убрать закладку' : 'Добавить закладку'"
                      :title="isBookmarked(turn.id) ? 'Убрать закладку' : 'Добавить закладку'"
                      @click="toggleBookmark(turn)"><span aria-hidden="true"></span></button>
                  </div>
                  <ChatMarkdown class="studio-response" :content="turn.body" />
                </template>
                <p v-else>{{ turn.body }}</p>
              </article>
            </div>
          </div>
        </div>
        <div class="studio-chat-footer">
          <form class="studio-composer studio-chat-composer" @submit.prevent="send">
            <label class="sr-only" for="studio-chat-text">Сообщение для Budz</label>
            <textarea id="studio-chat-text" v-model="draft" rows="1" placeholder="Продолжите мысль…"></textarea>
            <UiButton variant="primary" type="submit" :icon="Send" :disabled="!draft.trim()" aria-label="Отправить" />
          </form>
        </div>
      </section>

      <StudioAppearance v-else :saved="savedStyle" :mode="mode" :show-draft="initialDraft" @save="savedStyle = $event" />
    </main>
  </div>

  <dialog id="studio-history-panel" ref="historyPanel" class="studio-dialog studio-history-dialog" aria-label="История диалогов">
    <button type="button" class="studio-icon studio-close-history" aria-label="Закрыть историю" autofocus @click="historyPanel?.close()"><X aria-hidden="true" /></button>
    <StudioHistory :threads="threads" :active-id="currentId" :appearance-open="screen === 'appearance'"
      @start="navigate('start')" @select="openThread" @appearance="navigate('appearance')" />
  </dialog>

  <dialog id="studio-outline-panel" ref="outlinePanel" class="studio-dialog studio-outline" aria-labelledby="studio-outline-title">
    <header>
      <h2 id="studio-outline-title">Оглавление</h2>
      <button type="button" class="studio-icon" aria-label="Закрыть оглавление" autofocus @click="outlinePanel?.close()"><X aria-hidden="true" /></button>
    </header>
    <section aria-label="Ваши вопросы">
      <h3>Ваши вопросы</h3>
      <button v-for="turn in questions" :key="turn.id" type="button" @click="goToTurn(turn.id)">{{ turn.body.slice(0, 88) }}</button>
    </section>
    <section aria-label="Закладки">
      <h3>Закладки</h3>
      <p v-if="!markedTurns.length">Сохранённых закладок пока нет.</p>
      <button v-for="turn in markedTurns" :key="turn.id" class="studio-outline-bookmark" type="button" @click="goToTurn(turn.id)">
        <span class="studio-preview-dot" aria-hidden="true"></span>{{ turn.label || turn.body.slice(0, 72) }}
      </button>
    </section>
  </dialog>
  <dialog ref="rulesPanel" class="studio-dialog studio-rules" aria-labelledby="studio-rules-title">
    <header>
      <h2 id="studio-rules-title">Правила диалога</h2>
      <button type="button" class="studio-icon" aria-label="Закрыть правила" autofocus @click="rulesPanel?.close()"><X aria-hidden="true" /></button>
    </header>
    <p>Пример правила. Объяснять простым языком и приводить короткий пример.</p>
  </dialog>
  <UiToast />
</template>
