<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowDown, ArrowUp, BookOpen, Check, ChevronDown, Code, Copy, Ellipsis, FileImage, FileText, Lightbulb, ListChecks, PanelLeft, Paperclip, PenLine, Pencil, Pin, PinOff, Plus, RotateCcw, Search, Square, ThumbsDown, ThumbsUp, X } from 'lucide-vue-next'
import { categoryColors, entityColor, entityIcon } from '../appearance/entities'
import { appearance } from '../appearance'
import { api } from '../api'
import { useSession } from '../session'
import ChatMarkdown from '../components/ChatMarkdown.vue'
import ChatRulesPanel from '../components/ChatRulesPanel.vue'
import ProjectEditor from '../components/ProjectEditor.vue'
import UiAvatar from '../components/ui/UiAvatar.vue'
import UiButton from '../components/ui/UiButton.vue'
import UiChip from '../components/ui/UiChip.vue'
import UiIconButton from '../components/ui/UiIconButton.vue'
import { useChat } from '../use-chat'
import type { Chat, Message, Persona } from '../types'

const {
  chats, messages, selected, selectedId, model, currentPlan, search, nextBefore,
  chatsHaveMore, draft, files, busy, loading, error, linked, flow, lastUser, lastAnswer, adaptation, transcript,
  workspaces, workspaceId, workspaceQuery, currentWorkspace,
  projectChats, projectHasMore, projectLoading, loadProjectChats, loadWorkspaces, openProject, projectRemoved,
  loadChats, loadMessages, newChat, patch, move, remove, changeDraft, chooseFiles, removeFile, send, stop, revise, rate,
  decide, download,
} = useChat()
const compactLayout = window.matchMedia('(max-width: 780px)')
const compact = ref(compactLayout.matches)
const desktopHistory = ref(true)
const mobileHistory = ref(false)
const historyPanel = ref<HTMLElement>()
const historyToggle = ref<HTMLButtonElement>()
const historyVisible = computed(() => compact.value ? mobileHistory.value : desktopHistory.value)
const historyAction = computed(() => historyVisible.value ? 'Скрыть список диалогов' : 'Показать список диалогов')
const historyKey = 'budz.history'
try { desktopHistory.value = localStorage.getItem(historyKey) !== 'hidden' } catch { /* Use the default layout. */ }
const picker = ref<HTMLInputElement>()
const dialog = ref<HTMLDialogElement>()
const operation = ref<'rename' | 'delete' | 'edit'>('rename')
const modalText = ref('')
const copied = ref('')
const feedbackTarget = ref('')
const composer = ref<HTMLTextAreaElement>()
const chatMenu = ref<HTMLDetailsElement>()
const rulesPanel = ref<InstanceType<typeof ChatRulesPanel>>()
const projectEditor = ref<InstanceType<typeof ProjectEditor>>()
const temporary = computed(() => Boolean(selected.value?.expiresAt))
const projectNames = computed(() => new Map(workspaces.value.map((project) => [project.id, project.name])))
const session = useSession()
const personas = ref<Persona[]>([])
const personasById = computed(() => new Map(personas.value.map((persona) => [persona.id, persona])))
const activePersona = computed(() => selected.value?.personaId ? personasById.value.get(selected.value.personaId) : undefined)
const assistantIcon = computed(() => appearance.value.assistantAvatar === 'persona' ? entityIcon(activePersona.value?.icon) : undefined)
function personaColor(id: string | null | undefined) {
  return entityColor(id ? personasById.value.get(id)?.color : undefined, id ?? 'budz')
}
const numberFormat = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 })
const projectsOpen = ref(true)
const showLatest = ref(false)
const dragDepth = ref(0)
const draftLength = computed(() => draft.value.length)
const defaultIdeas = [
  { title: 'Разобраться в теме', prompt: 'Объясни идею на простом примере', icon: Lightbulb },
  { title: 'Написать код', prompt: 'Покажи, как решить задачу с помощью кода', icon: Code },
  { title: 'Улучшить текст', prompt: 'Помоги улучшить мой текст', icon: PenLine },
  { title: 'Составить план', prompt: 'Разложи задачу на шаги', icon: BookOpen },
]
const chatGroups = computed(() => {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const yesterday = new Date(today)
  yesterday.setDate(today.getDate() - 1)
  const groups = ['Закреплённые', 'Сегодня', 'Вчера', 'Ранее'].map((title) => ({ title, items: [] as Chat[] }))
  for (const chat of chats.value) {
    const date = Date.parse(chat.lastMessageAt)
    const group = chat.isPinned ? 0 : date >= +today ? 1 : date >= +yesterday ? 2 : 3
    groups[group].items.push(chat)
  }
  return groups.filter((group) => group.items.length)
})
watch(search, (_, __, onCleanup) => {
  const timer = window.setTimeout(() => void loadChats(), 250)
  onCleanup(() => window.clearTimeout(timer))
})
const reasons = { too_long: 'Слишком длинно', too_complex: 'Слишком сложно', inaccurate: 'Неточно', wrong_language: 'Не тот язык' }
const failureMessages: Record<string, string> = {
  ai_timeout: 'Модель не успела ответить.', ai_empty: 'Модель вернула пустой ответ.',
  ai_credentials: 'Сервис модели отклонил ключ.', ai_rate_limit: 'Сервис модели ограничил запросы.',
  generation_interrupted: 'Генерация прервалась после перезапуска.', ai_incomplete: 'Поток ответа оборвался.',
  daily_token_limit: 'Для этого сообщения не хватает токенов. Сократите запрос или проверьте бюджет на странице тарифов.',
}
const formTitle = computed(() => ({ rename: 'Переименовать диалог', delete: 'Удалить диалог?', edit: 'Изменить сообщение' })[operation.value])

function updateLatestButton() {
  const element = transcript.value
  showLatest.value = !!element && element.scrollHeight - element.clientHeight - element.scrollTop > 220
}
function jumpToLatest() {
  const element = transcript.value
  if (element) element.scrollTop = element.scrollHeight
}
function dragEnter(event: DragEvent) {
  if (temporary.value || !event.dataTransfer?.types.includes('Files')) return
  event.preventDefault()
  dragDepth.value++
}
function dragOver(event: DragEvent) {
  if (!event.dataTransfer?.types.includes('Files')) return
  event.preventDefault()
  event.dataTransfer.dropEffect = temporary.value ? 'none' : 'copy'
}
function dropFiles(event: DragEvent) {
  dragDepth.value = 0
  if (!temporary.value) chooseFiles(event.dataTransfer?.files ?? null)
}
function formatFileSize(size: number) {
  return size >= 1048576 ? `${numberFormat.format(size / 1048576)} МБ` : size >= 1024 ? `${numberFormat.format(size / 1024)} КБ` : `${size} Б`
}

function setComposer(text: string) {
  changeDraft(text)
  composer.value?.focus()
}
function showDialog(type: typeof operation.value) {
  if (chatMenu.value) chatMenu.value.open = false
  operation.value = type
  modalText.value = type === 'rename' ? selected.value?.title ?? '' : lastUser.value?.content ?? ''
  dialog.value?.showModal()
}
async function applyDialog() {
  if (operation.value === 'delete') { dialog.value?.close(); await remove(); return }
  const text = modalText.value.trim()
  if (!text) return
  const success = operation.value === 'rename' ? await patch({ title: text })
    : lastUser.value ? await revise(lastUser.value, text) : false
  if (success) dialog.value?.close()
}
async function copy(message: Message) {
  try { await navigator.clipboard.writeText(message.content); copied.value = message.id; setTimeout(() => { copied.value = '' }, 1400) }
  catch { error.value = 'Не удалось скопировать ответ.' }
}
function addedFiles(event: Event) {
  const input = event.target as HTMLInputElement
  chooseFiles(input.files)
  input.value = ''
}
function setModel(event: Event) { model.value = (event.target as HTMLSelectElement).value }

function moveChat(event: Event) {
  const workspace = (event.target as HTMLSelectElement).value
  if (chatMenu.value) chatMenu.value.open = false
  void move(workspace)
}

function editProject() {
  if (!currentWorkspace.value) return
  if (chatMenu.value) chatMenu.value.open = false
  projectEditor.value?.show(currentWorkspace.value)
}

function chatRoute(chat: Chat) {
  return { path: `/chats/${chat.id}`, query: chat.workspaceId ? { workspace: chat.workspaceId } : {} }
}

async function projectSaved(id: string, created: boolean) {
  await loadWorkspaces()
  if (created) {
    closeMobileHistory()
    await openProject(id)
  }
}

function time(value: string) {
  return new Intl.DateTimeFormat('ru', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function setHistory(visible: boolean) {
  if (compact.value) mobileHistory.value = visible
  else {
    desktopHistory.value = visible
    try { localStorage.setItem(historyKey, visible ? 'shown' : 'hidden') } catch { /* Keep the choice for this page. */ }
  }
}
async function toggleHistory() {
  const opening = !historyVisible.value
  setHistory(opening)
  if (opening) {
    await nextTick()
    historyPanel.value?.focus()
  }
}
function hideHistory() {
  setHistory(false)
  historyToggle.value?.focus()
}
function closeMobileHistory() {
  if (compact.value && mobileHistory.value) hideHistory()
}
function startNewChat() {
  closeMobileHistory()
  void newChat()
}
function resizeLayout(event: MediaQueryListEvent) {
  compact.value = event.matches
  mobileHistory.value = false
}
function resizeComposer() {
  const input = composer.value
  if (!input) return
  input.style.height = '0px'
  const css = getComputedStyle(input)
  input.style.height = `${Math.min(parseFloat(css.maxHeight), Math.max(parseFloat(css.minHeight), input.scrollHeight))}px`
}

let sizeObserver: ResizeObserver | undefined
watch(draft, resizeComposer, { flush: 'post' })
watch(selectedId, () => { showLatest.value = false })
onMounted(() => {
  void api<Persona[]>('/personas').then((items) => { personas.value = items }).catch(() => { /* Use the fallback colors. */ })
  compactLayout.addEventListener('change', resizeLayout)
  resizeComposer()
  if (!composer.value) return
  let previousWidth = composer.value.clientWidth
  sizeObserver = new ResizeObserver(() => {
    if (!composer.value || composer.value.clientWidth === previousWidth) return
    previousWidth = composer.value.clientWidth
    resizeComposer()
  })
  sizeObserver.observe(composer.value)
})
onUnmounted(() => {
  compactLayout.removeEventListener('change', resizeLayout)
  sizeObserver?.disconnect()
})
</script>

<template>
  <section class="workspace" :class="{ 'history-open': historyVisible }" aria-label="Диалоги Budz">
    <aside v-show="historyVisible" id="chat-history" ref="historyPanel" class="history" tabindex="-1" aria-label="Боковая панель диалогов" @keydown.esc.stop="hideHistory">
      <UiButton class="history-new" variant="primary" :icon="Plus" block aria-label="Создать диалог" @click="startNewChat">Новый диалог</UiButton>
      <section class="history-projects" aria-labelledby="projects-heading">
        <div class="history-project-heading">
          <h2 id="projects-heading"><RouterLink to="/workspaces" title="Управление проектами">Проекты</RouterLink></h2>
          <button type="button" class="history-project-add" :aria-expanded="projectsOpen" aria-controls="history-project-list"
            :aria-label="projectsOpen ? 'Свернуть проекты' : 'Развернуть проекты'" @click="projectsOpen = !projectsOpen"><ChevronDown :class="{ collapsed: !projectsOpen }" aria-hidden="true" /></button>
          <UiIconButton :icon="Plus" size="sm" label="Новый проект" @click="projectEditor?.show()" />
        </div>
        <nav v-if="workspaces.length" v-show="projectsOpen" id="history-project-list" class="history-project-list" aria-label="Список проектов">
          <RouterLink v-for="project in workspaces" :key="project.id" :to="{ path: '/chats', query: { workspace: project.id } }"
            :aria-current="workspaceId === project.id ? 'location' : undefined" @click="closeMobileHistory">
            <component :is="entityIcon(project.icon, 'folder')" class="project-icon"
              :style="{ color: `var(--${entityColor(project.color, project.id)})` }" aria-hidden="true" /><span>{{ project.name }}</span>
          </RouterLink>
        </nav>
      </section>
      <h2>Все диалоги</h2>
      <div class="search-form">
        <Search class="search-icon" aria-hidden="true" />
        <label class="sr-only" for="chat-search">Поиск по диалогам</label>
        <input id="chat-search" v-model="search" type="search" maxlength="120" placeholder="Поиск по диалогам">
      </div>
      <nav aria-label="История диалогов" class="thread-list">
        <section v-for="group in chatGroups" :key="group.title" class="thread-group" :aria-label="group.title">
          <h3>{{ group.title }}</h3>
          <div v-for="chat in group.items" :key="chat.id" class="thread-row">
            <RouterLink :to="chatRoute(chat)" @click="closeMobileHistory">
              <span class="persona-dot" :style="{ backgroundColor: `var(--${personaColor(chat.personaId)})` }" aria-hidden="true"></span>
              <span><span>{{ chat.title }}</span><small v-if="chat.workspaceId && projectNames.has(chat.workspaceId)">{{ projectNames.get(chat.workspaceId) }}</small></span>
            </RouterLink>
            <UiIconButton class="thread-pin" :icon="chat.isPinned ? PinOff : Pin" :label="chat.isPinned ? 'Открепить диалог' : 'Закрепить диалог'"
              size="sm" @click="patch({ isPinned: !chat.isPinned }, chat.id)" />
          </div>
        </section>
        <p v-if="!chats.length" class="muted">Диалогов пока нет.</p>
      </nav>
      <button v-if="chatsHaveMore" class="plain-button" @click="loadChats(false)">Ещё диалоги</button>
    </aside>

    <div class="conversation">
      <header class="conversation-header">
        <button ref="historyToggle" type="button" class="chat-control history-toggle" :aria-label="historyAction" :title="historyAction"
          :aria-expanded="historyVisible" aria-controls="chat-history" @click="toggleHistory">
          <PanelLeft aria-hidden="true" />
        </button>
        <div class="conversation-heading">
          <h1>{{ selected?.title ?? (selectedId ? 'Диалог' : currentWorkspace?.name ?? 'Новый диалог') }}</h1>
          <p v-if="selected?.expiresAt" class="help chat-scope-name" :title="new Date(selected.expiresAt).toLocaleString('ru-RU')">Временный диалог</p>
          <p v-else-if="selectedId && currentWorkspace" class="help chat-scope-name">{{ currentWorkspace.name }}</p>
          <p v-if="selectedId" class="status-line"><span :class="{ online: linked }"></span>{{ linked ? 'Подключено' : 'Подключаемся к чату' }}</p>
        </div>
        <button type="button" class="chat-control chat-rules-button" aria-haspopup="dialog" @click="rulesPanel?.show()">Правила</button>
        <button type="button" class="chat-control new-chat-control" aria-label="Новый диалог" title="Новый диалог" @click="startNewChat"><Plus aria-hidden="true" /></button>
        <details ref="chatMenu" class="chat-menu">
          <summary aria-label="Настройки диалога"><Ellipsis aria-hidden="true" /></summary>
          <div class="chat-menu-panel">
            <label>Модель <select :value="model" aria-label="Модель" @change="setModel">
              <option value="">Автоматически</option><option v-for="allowed in currentPlan?.allowedModels" :key="allowed" :value="allowed">{{ allowed }}</option>
            </select></label>
            <button v-if="currentWorkspace && selectedId" class="text-button" @click="editProject">Настройки проекта</button>
            <template v-if="selectedId">
              <label v-if="!temporary && workspaces.length" :key="workspaceId + ':' + selectedId">Переместить диалог
                <select :value="workspaceId" aria-label="Переместить диалог" :disabled="!!flow" @change="moveChat">
                  <option value="">Личные диалоги</option>
                  <option v-for="space in workspaces" :key="space.id" :value="space.id">{{ space.name }}</option>
                </select>
              </label>
              <RouterLink v-if="!temporary" class="text-button" :to="{ path: '/data', query: { chat: selectedId, ...workspaceQuery } }">Экспорт диалога</RouterLink>
              <button v-if="!temporary" class="text-button" @click="patch({ isPinned: !selected?.isPinned })">{{ selected?.isPinned ? 'Открепить' : 'Закрепить' }}</button>
              <button class="text-button" @click="showDialog('rename')">Переименовать</button>
              <button class="text-button danger-text" @click="showDialog('delete')">Удалить</button>
            </template>
          </div>
        </details>
      </header>

      <div ref="transcript" class="transcript" aria-label="Сообщения" @scroll.passive="updateLatestButton">
        <p v-if="loading && !messages.length" class="muted" role="status">Загружаем сообщения…</p>
        <button v-if="nextBefore" class="plain-button" @click="loadMessages(false, true)">Загрузить ранние сообщения</button>
        <section v-if="currentWorkspace && !selectedId" class="project-overview stack" aria-labelledby="project-conversations">
          <header class="project-overview-heading">
            <h2 id="project-conversations">Диалоги проекта</h2>
            <div class="actions">
              <button type="button" class="secondary" @click="editProject">Настройки проекта</button>
              <RouterLink :to="{ path: '/data', query: workspaceQuery }" class="text-button">Перенос данных</RouterLink>
            </div>
          </header>
          <nav v-if="projectChats.length" class="thread-list project-conversations" aria-label="Диалоги проекта">
            <RouterLink v-for="chat in projectChats" :key="chat.id" :to="chatRoute(chat)">
              <Pin v-if="chat.isPinned" class="project-icon" aria-label="Закреплён" />{{ chat.title }}
            </RouterLink>
          </nav>
          <p v-else-if="!projectLoading" class="muted">В проекте пока нет диалогов.</p>
          <p v-if="projectLoading" class="muted" role="status">Загружаем диалоги проекта…</p>
          <button v-else-if="projectHasMore" type="button" class="plain-button self-start" @click="loadProjectChats(false)">Ещё диалоги проекта</button>
        </section>
        <div v-else-if="!loading && !messages.length" class="start-conversation">
          <svg class="start-illustration" viewBox="0 0 84 64" aria-hidden="true" fill="none">
            <rect x="4" y="5" width="60" height="44" rx="13" fill="var(--surface-3)" stroke="var(--accent)" />
            <path d="M27 31h23m-23-9h15M51 49l10 10V46" stroke="var(--accent)" stroke-width="2" stroke-linecap="round" />
            <path d="M69 17h12m-6-6v12" stroke="var(--accent-2)" stroke-width="2" stroke-linecap="round" />
          </svg>
          <h2>{{ session.account?.username ? `Привет, ${session.account.username}!` : 'С чего начнём?' }}</h2>
          <div class="start-choices">
            <button v-for="(idea, index) in defaultIdeas" :key="idea.title" type="button" class="idea-card"
              :style="{ '--idea-color': `var(--${categoryColors[index]})` }" @click="setComposer(idea.prompt)">
              <component :is="idea.icon" aria-hidden="true" /><strong>{{ idea.title }}</strong><span>{{ idea.prompt }}</span>
            </button>
          </div>
        </div>
        <article v-for="item in messages" :key="item.id" class="message" :class="item.role">
          <div class="message-meta">
            <UiAvatar v-if="item.role === 'user'" :name="session.account?.username ?? 'Вы'" size="sm" decorative />
            <UiAvatar v-else :name="activePersona?.name ?? 'Budz'" :brand="appearance.assistantAvatar !== 'persona'"
              :icon="assistantIcon" :color="personaColor(activePersona?.id)" size="sm" decorative />
            <strong>{{ item.role === 'user' ? 'Вы' : 'Budz' }}</strong>
            <time :datetime="item.createdAt">{{ time(item.createdAt) }}</time>
          </div>
          <div class="message-body">
            <ChatMarkdown v-if="item.content" :content="item.content" :class="{ streaming: item.status === 'streaming' }" />
            <p v-if="!item.content && (item.status === 'pending' || item.status === 'streaming')" class="typing muted" role="status">
              Budz печатает <span aria-hidden="true"><i></i><i></i><i></i></span>
            </p>
            <p v-if="item.status === 'failed'" class="muted" role="status">{{ failureMessages[item.errorCode ?? ''] ?? 'Ответ недоступен.' }} Можно повторить генерацию.</p>
            <p v-if="item.status === 'stopped'" class="muted" role="status">Генерация остановлена.</p>
            <ul v-if="item.attachments?.length" class="file-list">
              <li v-for="attachment in item.attachments" :key="attachment.id"><button class="text-button" @click="download(attachment)">Скачать {{ attachment.fileName }}</button></li>
            </ul>
          </div>
          <div v-if="item.status !== 'pending' && item.status !== 'streaming'" class="message-actions">
            <UiIconButton v-if="item.rules" :icon="ListChecks" label="Правила ответа" @click="rulesPanel?.show(item.rules)" />
            <UiIconButton v-if="item.content" :icon="copied === item.id ? Check : Copy" :label="copied === item.id ? 'Скопировано' : 'Копировать'" @click="copy(item)" />
            <UiIconButton v-if="item.id === lastUser?.id && !flow" :icon="Pencil" label="Изменить" @click="showDialog('edit')" />
            <UiIconButton v-if="item.id === lastAnswer?.id && !flow" :icon="RotateCcw" label="Повторить" :disabled="busy" @click="revise(item)" />
            <template v-if="!temporary && item.role === 'assistant' && item.status === 'completed'">
              <UiIconButton :icon="ThumbsUp" label="Полезно" :pressed="item.rating === 1" @click="rate(item, 1)" />
              <UiIconButton :icon="ThumbsDown" label="Не помогло" :pressed="item.rating === -1" @click="feedbackTarget = feedbackTarget === item.id ? '' : item.id" />
            </template>
          </div>
          <div v-if="feedbackTarget === item.id" class="feedback-reasons" aria-label="Причина оценки">
            <button v-for="(label, code) in reasons" :key="code" class="secondary" @click="rate(item, -1, code); feedbackTarget = ''">{{ label }}</button>
            <button class="text-button" @click="rate(item, -1); feedbackTarget = ''">Без причины</button>
          </div>
          <div v-if="item.id === lastAnswer?.id && item.suggestions.length" class="suggestions" aria-label="Вопросы для продолжения">
            <UiChip v-for="(idea, index) in item.suggestions" :key="idea" :category="categoryColors[index % categoryColors.length]"
              :icon="Lightbulb" arrow @click="setComposer(idea)">{{ idea }}</UiChip>
          </div>
        </article>
      </div>

      <div class="composer-wrap">
        <UiButton v-if="showLatest" class="jump-latest" size="sm" :icon="ArrowDown" @click="jumpToLatest">Вниз к новым</UiButton>
        <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
        <div v-if="adaptation && !temporary" class="notice adaptation" role="status">
          <span>Последние оценки предлагают сделать ответы короче.</span>
          <button class="text-button" @click="decide(true)">Применить</button>
          <button class="text-button" @click="decide(false)">Скрыть</button>
        </div>
        <form class="composer" :class="{ 'text-only': temporary, dragging: dragDepth > 0 }" @submit.prevent="send"
          @dragenter="dragEnter" @dragover="dragOver" @dragleave="dragDepth = Math.max(0, dragDepth - 1)" @drop.prevent="dropFiles">
          <span v-if="dragDepth > 0" class="drop-label" role="status">Отпустите, чтобы прикрепить</span>
          <input ref="picker" class="sr-only" type="file" tabindex="-1" aria-label="Выбор файла" multiple accept=".txt,.md,.pdf,.docx,.png,.jpg,.jpeg" @change="addedFiles">
          <button v-if="!temporary" type="button" class="composer-attach" aria-label="Прикрепить" title="Прикрепить" @click="picker?.click()"><Paperclip aria-hidden="true" /></button>
          <label class="sr-only" for="composer">Сообщение для Budz</label>
          <textarea id="composer" ref="composer" :value="draft" placeholder="Сообщение для Budz…" rows="1" maxlength="10000"
            @input="changeDraft(($event.target as HTMLTextAreaElement).value)" @keydown.ctrl.enter.prevent="send"></textarea>
          <button v-if="flow" type="button" class="primary composer-action stop-button" aria-label="Остановить" title="Остановить" @click="stop"><Square aria-hidden="true" /></button>
          <button v-else type="submit" class="primary composer-action send-button" aria-label="Отправить" title="Отправить" :disabled="busy || (!draft.trim() && !files.length)"><ArrowUp aria-hidden="true" /></button>
          <small v-if="draftLength >= 8000" class="composer-count">{{ numberFormat.format(draftLength) }} / 10 000 символов · ≈ {{ numberFormat.format(Math.ceil(draftLength / 4)) }} токенов</small>
          <ul v-if="files.length" class="file-list picked">
            <li v-for="(file, index) in files" :key="`${file.name}-${index}`">
              <component :is="file.type.startsWith('image/') ? FileImage : FileText" class="project-icon" aria-hidden="true" />
              <span>{{ file.name }}</span><small>{{ formatFileSize(file.size) }}</small>
              <UiIconButton :icon="X" size="sm" :label="`Удалить ${file.name}`" @click="removeFile(index)" />
            </li>
          </ul>
        </form>
      </div>
    </div>

    <dialog ref="dialog" aria-labelledby="chat-dialog-title">
      <form class="stack" @submit.prevent="applyDialog">
        <h2 id="chat-dialog-title">{{ formTitle }}</h2>
        <p v-if="operation === 'delete'">Переписка и файлы этого диалога будут удалены.</p>
        <label v-else-if="operation === 'rename'">Название <input v-model="modalText" maxlength="120" required autofocus></label>
        <label v-else>Сообщение <textarea v-model="modalText" maxlength="10000" rows="5" required autofocus></textarea></label>
        <div class="actions">
          <button type="button" class="secondary" @click="dialog?.close()">Отмена</button>
          <button type="submit" :class="operation === 'delete' ? 'danger-button' : 'primary'" :disabled="busy">{{ operation === 'delete' ? 'Удалить' : 'Сохранить' }}</button>
        </div>
      </form>
    </dialog>
    <ChatRulesPanel ref="rulesPanel" :chat-id="selectedId" :workspace-id="workspaceId" :last="lastAnswer?.rules" />
    <ProjectEditor ref="projectEditor" @saved="projectSaved" @removed="projectRemoved" />
  </section>
</template>

<style scoped>
.history-new { margin-bottom: var(--space-3); }
.history-project-heading h2 { flex: 1; }
.history-project-add { background: transparent; }
.history-project-add svg { transition: transform var(--motion-base) var(--ease-out); }
.history-project-add .collapsed { transform: rotate(-90deg); }
.search-form { position: relative; }
.search-icon { position: absolute; left: 10px; width: 16px; height: 16px; color: var(--text-muted); pointer-events: none; }
.search-form input { padding-inline-start: 33px; }
.thread-group h3 { margin: var(--space-2) 0 var(--space-1); padding-inline: var(--space-2); color: var(--text-muted);
  font-size: var(--font-size-xs); font-weight: 650; }
.thread-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; }
.thread-row a { align-items: baseline; transition: transform var(--motion-fast), background-color var(--motion-fast); }
.thread-row a:hover { transform: translateX(2px); }
.thread-pin { justify-self: end; }
@media (hover: hover) {
  .thread-row:not(:hover, :focus-within) .thread-pin { opacity: 0; }
}
.persona-dot { width: 8px; height: 8px; border-radius: var(--radius-full); flex: 0 0 auto; }
.project-icon, .history-project-add svg, .chat-control svg, .chat-menu > summary svg,
.composer-action svg, .composer-attach svg { width: 17px; height: 17px; flex: 0 0 auto; }
.history-toggle[aria-expanded="true"] { background: var(--surface-3); border-color: var(--accent); }
.history { animation: history-in var(--motion-base) var(--ease-out) both; }
@keyframes history-in { from { opacity: 0; transform: translateX(-6px); } to { opacity: 1; transform: translateX(0); } }
.conversation { position: relative; --chat-content-width: 810px; }
:global(:root[data-chat-width="narrow"] .workspace .conversation) { --chat-content-width: 610px; }
:global(:root[data-chat-width="wide"] .workspace .conversation) { --chat-content-width: 1100px; }
.transcript, .composer-wrap { padding-inline: max(12px, calc((100% - var(--chat-content-width)) / 2)); }
.start-illustration { width: 76px; height: auto; }
.start-choices { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); width: 100%; }
.start-choices .idea-card { display: grid; justify-items: start; gap: var(--space-1); padding: var(--space-3);
  min-width: 0; text-align: start; overflow-wrap: anywhere; border: 1px solid var(--border); border-inline-start: 3px solid var(--idea-color);
  border-radius: var(--radius-md); background: var(--surface-1); color: var(--text);
  transition: background-color var(--motion-base), transform var(--motion-fast), box-shadow var(--motion-base); }
.start-choices .idea-card:hover { transform: translateY(-2px); box-shadow: var(--elevation-2); background: var(--surface-2); }
.start-choices .idea-card svg { width: 18px; height: 18px; color: var(--accent); }
.start-choices .idea-card span { color: var(--text-muted); font-size: var(--font-size-xs); }
.message { animation: message-in var(--motion-base) var(--ease-out) both; }
@keyframes message-in { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: translateY(0); } }
:global(:root[data-message-style="bubbles"] .workspace .message) { grid-template-columns: minmax(0, 1fr); width: fit-content;
  max-width: 90%; border-radius: var(--radius-xl); }
:global(:root[data-message-style="bubbles"] .workspace .message > :is(.message-actions, .suggestions, .feedback-reasons)) { grid-column: 1; }
:global(:root[data-user-side="right"] .workspace .message.user) { margin-inline-start: auto; }
:global(:root[data-user-side="right"]:not([data-message-style="bubbles"]) .workspace .message.user) { width: 94%; }
:global(:root[data-message-style="flat"] .workspace .message) { background: none; border: 0; border-bottom: 1px solid var(--border); border-radius: 0; }
.message-actions { gap: var(--space-1); }
@media (hover: hover) {
  :global(:root[data-message-actions="hover"] .workspace .message:not(:hover, :focus-within) .message-actions) { opacity: 0; }
}
.typing { display: inline-flex; align-items: center; gap: var(--space-2); }
.typing span { display: inline-flex; gap: 3px; }
.typing i { width: 4px; height: 4px; border-radius: var(--radius-full); background: var(--accent-2);
  animation: cursor-pulse 900ms ease-in-out alternate infinite; }
.typing i:nth-child(2) { animation-delay: 150ms; }
.typing i:nth-child(3) { animation-delay: 300ms; }
.streaming::after { content: ''; display: inline-block; height: 1em; width: 2px; background: var(--accent-2);
  animation: cursor-pulse 700ms alternate infinite; }
@keyframes cursor-pulse { from { opacity: .35; } to { opacity: 1; } }
.composer-wrap { position: relative; }
.jump-latest { position: absolute; right: var(--space-4); bottom: calc(100% + var(--space-2)); z-index: 1;
  background: var(--surface-1); box-shadow: var(--elevation-2); }
.composer { position: relative; transition: box-shadow var(--motion-base), border-color var(--motion-base); }
.composer:focus-within, .composer.dragging { border-color: var(--accent); box-shadow: var(--glow-sm); }
.composer .send-button { background-image: linear-gradient(145deg, var(--btn-primary-bg), var(--contrast-fill)); }
.send-button:not(:disabled):active { transform: scale(.94); }
.drop-label { position: absolute; inset: -1px; z-index: 1; display: grid; place-items: center; padding: var(--space-2);
  pointer-events: none; background: var(--surface-3); border: 2px dashed var(--accent); border-radius: inherit; color: var(--text); }
.composer-count { grid-column: 1 / -1; justify-self: end; color: var(--text-muted); }
.file-list.picked li { max-width: 100%; }
.file-list.picked small { white-space: nowrap; color: var(--text-muted); }
@media (max-width: 780px) {
  .chat-control, .history-project-add { min-width: 40px; min-height: 40px; }
  .chat-menu > summary { min-height: 40px; }
  .composer { grid-template-columns: 40px minmax(0, 1fr) 40px; }
  .composer.text-only { grid-template-columns: minmax(0, 1fr) 40px; }
  .composer-attach, .composer-action { width: 40px; height: 40px; }
}
@media (max-width: 390px) {
  .start-choices { grid-template-columns: minmax(0, 1fr); }
}
</style>
