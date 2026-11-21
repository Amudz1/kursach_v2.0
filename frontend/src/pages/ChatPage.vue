<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowDown, Check, ChevronDown, Copy, Ellipsis, FileImage, FileText, Folder, Lightbulb, ListChecks, PanelLeft, Paperclip, Pencil, Pin, PinOff, Plus, RotateCcw, Search, Send, Square, ThumbsDown, ThumbsUp, X } from 'lucide-vue-next'
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
const compactLayout = window.matchMedia('(max-width: 900px)')
const compact = ref(compactLayout.matches)
const desktopHistory = ref(true)
const mobileHistory = ref(false)
const historyDialog = ref<HTMLDialogElement>()
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
const viewportHeight = ref<string>()
const temporary = computed(() => Boolean(selected.value?.expiresAt))
const projectNames = computed(() => new Map(workspaces.value.map((project) => [project.id, project.name])))
const projectMark = computed(() => {
  const project = currentWorkspace.value
  if (project) return { icon: entityIcon(project.icon, 'folder'), color: `var(--${entityColor(project.color, project.id)})` }
})
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
const defaultIdeas = ['Разобрать сложную тему', 'Составить план', 'Поработать над текстом']
const studioSuggestionColors = ['cat-7', 'cat-4'] as const
const suggestionColors = computed(() => appearance.value.preset === 'studio' ? studioSuggestionColors : categoryColors)
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

function showRules() {
  if (chatMenu.value) {
    chatMenu.value.open = false
    chatMenu.value.querySelector('summary')?.focus({ preventScroll: true })
  }
  rulesPanel.value?.show()
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
  if (compact.value) {
    if (visible) historyDialog.value?.showModal()
    else historyDialog.value?.close()
    mobileHistory.value = visible
    return
  }
  desktopHistory.value = visible
  try { localStorage.setItem(historyKey, visible ? 'shown' : 'hidden') } catch { /* Keep the choice for this page. */ }
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
  historyDialog.value?.close()
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

function resizeViewport() {
  const viewport = window.visualViewport
  viewportHeight.value = viewport?.scale === 1 ? `${viewport.height}px` : undefined
}

let sizeObserver: ResizeObserver | undefined
watch(draft, resizeComposer, { flush: 'post' })
watch(selectedId, () => { showLatest.value = false })
onMounted(() => {
  void api<Persona[]>('/personas').then((items) => { personas.value = items }).catch(() => { /* Use the fallback colors. */ })
  compactLayout.addEventListener('change', resizeLayout)
  window.visualViewport?.addEventListener('resize', resizeViewport)
  resizeViewport()
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
  window.visualViewport?.removeEventListener('resize', resizeViewport)
  sizeObserver?.disconnect()
})
</script>

<template>
  <section class="workspace" :class="{ 'history-open': historyVisible }" :style="{ '--chat-viewport-height': viewportHeight }" aria-label="Диалоги Budz">
    <dialog id="mobile-chat-history" ref="historyDialog" class="mobile-history" aria-label="История диалогов"
      @close="mobileHistory = !!historyDialog?.open" @click.self="historyDialog?.close()"></dialog>
    <Teleport to="#mobile-chat-history" :disabled="!compact || !mobileHistory" defer>
      <aside v-show="historyVisible" id="chat-history" ref="historyPanel" class="history" tabindex="-1" aria-label="Боковая панель диалогов" @keydown.esc.stop.prevent="hideHistory">
        <div class="history-tools">
          <div class="history-toolbar">
            <UiButton class="history-new" variant="secondary" :icon="Plus" block @click="startNewChat">Новый диалог</UiButton>
            <UiIconButton v-if="compact" :icon="X" label="Закрыть историю" @click="hideHistory" />
          </div>
          <div class="search-form">
            <Search class="search-icon" aria-hidden="true" />
            <label class="sr-only" for="chat-search">Поиск по диалогам</label>
            <input id="chat-search" v-model="search" type="search" maxlength="120" placeholder="Поиск по диалогам">
          </div>
        </div>
        <section class="history-projects" aria-labelledby="projects-heading">
          <div class="history-project-heading">
            <h2 id="projects-heading"><RouterLink to="/workspaces" title="Управление проектами"><Folder aria-hidden="true" />Проекты</RouterLink></h2>
            <button type="button" class="history-project-add" :aria-expanded="projectsOpen" aria-controls="history-project-list"
              :aria-label="projectsOpen ? 'Свернуть проекты' : 'Развернуть проекты'" @click="projectsOpen = !projectsOpen"><ChevronDown :class="{ collapsed: !projectsOpen }" aria-hidden="true" /></button>
            <UiIconButton :icon="Plus" size="sm" label="Новый проект" @click="projectEditor?.show()" />
          </div>
          <nav v-if="workspaces.length" v-show="projectsOpen" id="history-project-list" class="history-project-list" aria-label="Список проектов">
            <RouterLink v-for="project in workspaces" :key="project.id" :to="{ path: '/chats', query: { workspace: project.id } }"
              :aria-current="workspaceId === project.id ? 'location' : undefined" @click="closeMobileHistory">
              <component :is="entityIcon(project.icon, 'folder')" class="project-icon"
                :style="{ '--history-entity-color': `var(--${entityColor(project.color, project.id)})` }" aria-hidden="true" /><span>{{ project.name }}</span>
            </RouterLink>
          </nav>
        </section>
        <h2 class="sr-only">Все диалоги</h2>
        <nav aria-label="История диалогов" class="thread-list">
          <section v-for="group in chatGroups" :key="group.title" class="thread-group" :aria-label="group.title">
            <h3>{{ group.title }}</h3>
            <div v-for="chat in group.items" :key="chat.id" class="thread-row">
              <RouterLink :to="chatRoute(chat)" @click="closeMobileHistory">
                <span class="persona-dot" :style="{ '--history-entity-color': `var(--${personaColor(chat.personaId)})` }" aria-hidden="true"></span>
                <span class="thread-title"><span>{{ chat.title }}</span><small v-if="chat.workspaceId && projectNames.has(chat.workspaceId)">{{ projectNames.get(chat.workspaceId) }}</small></span>
              </RouterLink>
              <UiIconButton class="thread-pin" :icon="chat.isPinned ? PinOff : Pin" :label="chat.isPinned ? 'Открепить диалог' : 'Закрепить диалог'"
                size="sm" @click="patch({ isPinned: !chat.isPinned }, chat.id)" />
            </div>
          </section>
          <p v-if="!chats.length" class="muted">Диалогов пока нет.</p>
        </nav>
        <button v-if="chatsHaveMore" class="plain-button" @click="loadChats(false)">Ещё диалоги</button>
      </aside>
    </Teleport>

    <div class="conversation">
      <header class="conversation-header">
        <button ref="historyToggle" type="button" class="chat-control history-toggle" :aria-label="historyAction" :title="historyAction"
          :aria-expanded="historyVisible" :aria-haspopup="compact ? 'dialog' : undefined"
          :aria-controls="compact ? 'mobile-chat-history' : 'chat-history'" @click="toggleHistory">
          <PanelLeft aria-hidden="true" />
        </button>
        <div class="conversation-heading">
          <span>{{ selected?.title ?? currentWorkspace?.name ?? 'Диалоги' }}</span>
        </div>
        <details ref="chatMenu" class="chat-menu">
          <summary aria-label="Настройки диалога"><Ellipsis aria-hidden="true" /></summary>
          <div class="chat-menu-panel">
            <button type="button" class="text-button" aria-haspopup="dialog" @click="showRules">Правила</button>
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

      <div class="chat-content" :class="{ 'is-start': !selectedId }">
        <div v-if="!selectedId" class="chat-start">
          <p v-if="currentWorkspace" class="chat-scope-name">
            <component :is="projectMark?.icon" :style="{ '--project-mark': projectMark?.color }" aria-hidden="true" />{{ currentWorkspace.name }}
          </p>
          <h1>Что хотите<br class="start-break"> разобрать<span>?</span></h1>
        </div>
        <div v-show="selectedId" ref="transcript" class="transcript" aria-label="Сообщения" @scroll.passive="updateLatestButton">
          <header v-if="selectedId" class="reading-heading">
            <p v-if="selected?.expiresAt" class="chat-scope-name" :title="new Date(selected.expiresAt).toLocaleString('ru-RU')">Временный диалог</p>
            <p v-else-if="currentWorkspace" class="chat-scope-name">
              <component :is="projectMark?.icon" :style="{ '--project-mark': projectMark?.color }" aria-hidden="true" />{{ currentWorkspace.name }}
            </p>
            <h1>{{ selected?.title ?? 'Диалог' }}</h1>
            <p class="status-line"><span :class="{ online: linked }"></span>{{ linked ? 'Подключено' : 'Подключаемся к чату' }}</p>
          </header>
          <p v-if="loading && !messages.length" class="muted" role="status">Загружаем сообщения…</p>
          <button v-if="nextBefore" class="plain-button" @click="loadMessages(false, true)">Загрузить ранние сообщения</button>
          <div v-if="selectedId && !loading && !messages.length" class="start-choices" aria-label="Примеры запросов">
            <button v-for="idea in defaultIdeas" :key="idea" type="button" class="secondary" @click="setComposer(idea)">{{ idea }}</button>
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
                <UiChip v-for="(idea, index) in item.suggestions" :key="idea" :category="suggestionColors[index % suggestionColors.length]"
                :icon="Lightbulb" arrow @click="setComposer(idea)">{{ idea }}</UiChip>
            </div>
          </article>
        </div>

        <div class="composer-wrap">
          <UiButton v-if="selectedId && showLatest" class="jump-latest" size="sm" :icon="ArrowDown" @click="jumpToLatest">К последнему сообщению</UiButton>
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
            <textarea id="composer" ref="composer" :value="draft" :placeholder="selectedId ? 'Продолжите мысль…' : 'Напишите вопрос или мысль…'" rows="1" maxlength="10000"
              @input="changeDraft(($event.target as HTMLTextAreaElement).value)" @keydown.ctrl.enter.prevent="send"></textarea>
            <button v-if="flow" type="button" class="primary composer-action stop-button" aria-label="Остановить" title="Остановить" @click="stop"><Square aria-hidden="true" /></button>
            <button v-else type="submit" class="primary composer-action send-button" aria-label="Отправить" title="Отправить" :disabled="busy || (!draft.trim() && !files.length)"><Send aria-hidden="true" /><span v-if="!selectedId">Отправить</span></button>
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
        <div v-if="!selectedId" class="chat-start-extra">
          <section v-if="currentWorkspace" class="project-overview stack" aria-labelledby="project-conversations">
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
          <div v-else class="start-choices" aria-label="Примеры запросов">
            <button v-for="idea in defaultIdeas" :key="idea" type="button" class="secondary" @click="setComposer(idea)">{{ idea }}</button>
          </div>
        </div>
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
.workspace { display: grid; grid-template-columns: minmax(0, 1fr); height: calc(100dvh - var(--header)); background: var(--surface-0); }
.workspace.history-open { grid-template-columns: clamp(220px, 19vw, 266px) minmax(0, 1fr); }
.history {
  min-width: 0; min-height: 0; overflow: auto; overscroll-behavior: contain; padding: 0 var(--space-4) var(--space-4);
  scroll-padding-block-start: calc(84px + var(--space-2) + var(--space-6) + var(--space-3));
  background: var(--panel-surface); color: var(--text); border-inline-end: 1px solid var(--border);
}
.history:focus { outline: none; }
.history-tools { position: sticky; inset-block-start: 0; z-index: 2; display: grid; gap: var(--space-2);
  padding-block: var(--space-6) var(--space-3); background: var(--panel-surface); }
.history-toolbar { display: flex; align-items: center; gap: var(--space-2); }
.history .history-new { min-height: 42px; justify-content: flex-start; color: inherit; background: var(--sheet);
  border-color: var(--dark-rule); }
.history .history-new:hover { background: var(--panel-active); border-color: var(--focus); }
.history :deep(.ui-icon-button) { color: var(--text); }
.history :deep(.ui-icon-button:hover) { color: var(--focus); background: var(--panel-active); }
.history-projects { margin-block: var(--space-2) var(--space-4); padding-block-end: var(--space-2); border-bottom: 1px solid var(--border); }
.history-project-heading { display: flex; align-items: center; gap: var(--space-1); }
.history-project-heading h2 { flex: 1; font: 650 var(--font-size-sm)/1.4 var(--font-sans); }
.history-project-heading h2 a { display: flex; align-items: center; min-height: 40px; gap: var(--space-2); color: inherit; text-decoration: none; }
.history-project-heading h2 a:hover { text-decoration: underline; text-underline-offset: .2em; }
.history-project-heading h2 svg { width: 16px; height: 16px; }
.history-project-add { flex: 0 0 auto; display: grid; place-items: center; width: 32px; height: 32px; padding: 0;
  color: inherit; background: transparent; border: 1px solid transparent; border-radius: var(--radius-sm); }
.history-project-add:hover { background: var(--panel-active); }
.history-project-add svg { transition: transform var(--motion-base) var(--ease-out); }
.history-project-add .collapsed { transform: rotate(-90deg); }
.history-project-list { display: grid; gap: var(--space-1); max-height: min(28vh, 17rem); overflow: auto; }
.history-project-list a, .thread-row a { display: flex; align-items: center; gap: var(--space-2); min-height: 40px; min-width: 0;
  color: inherit; padding: var(--space-2); text-decoration: none; border-radius: var(--radius-md); overflow-wrap: anywhere; }
.history-project-list a:is(:hover, [aria-current]), .thread-row:is(:hover, :focus-within) { background: var(--panel-active); }
.history-project-list a:focus-visible, .thread-row a:focus-visible { outline-offset: -2px; }
.history-project-list .project-icon, .persona-dot {
  --history-marker: color-mix(in srgb, var(--history-entity-color), var(--text) 52%);
  color: var(--history-marker);
}
.search-form { position: relative; display: grid; align-items: center; }
.search-icon { position: absolute; inset-inline-start: 10px; width: 16px; height: 16px; color: var(--text-muted); pointer-events: none; }
.search-form input { min-height: 42px; padding-inline-start: 33px; color: inherit; background: var(--input); border-color: var(--dark-rule); }
.search-form input::placeholder { color: var(--text-muted); }
.thread-group h3 { margin: var(--space-3) 0 var(--space-1); padding-inline: var(--space-2); color: var(--text-muted);
  font: 650 var(--font-size-xs)/1.4 var(--font-sans); }
.thread-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; border-radius: var(--radius-md); }
.thread-row a { padding-inline-end: var(--space-1); background: transparent; box-shadow: none; }
.thread-row:has(a[aria-current]) { background: var(--panel-active); box-shadow: inset 3px 0 var(--focus); }
.thread-row a[aria-current] { font-weight: 700; }
.thread-title { min-width: 0; }
.thread-title > span, .thread-title small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.thread-title small, .history .muted, .history .plain-button { color: var(--text-muted); }
.history .plain-button:hover { background: var(--panel-active); }
.thread-pin { justify-self: end; margin-inline-end: 2px; }
@media (hover: hover) {
  .thread-row:not(:hover, :focus-within) .thread-pin { opacity: 0; }
}
.persona-dot { width: 8px; height: 8px; background: currentColor; border-radius: var(--radius-full); flex: 0 0 auto; }
.project-icon, .history-project-add svg, .chat-control svg, .chat-menu > summary svg,
.composer-action svg, .composer-attach svg { width: 17px; height: 17px; flex: 0 0 auto; }
.mobile-history { inset: 0 auto 0 0; width: min(88vw, 330px); height: 100dvh; max-width: none; max-height: none;
  margin: 0; padding: 0; overflow: hidden; background: var(--panel-surface); border: 0; border-radius: 0; }
.mobile-history .history { height: 100%; }
.conversation { display: grid; grid-template-columns: minmax(0, 1fr); grid-template-rows: auto minmax(0, 1fr);
  min-width: 0; min-height: 0; --chat-content-width: 702px; }
.conversation-header { display: grid; grid-template-columns: 42px minmax(0, 1fr) auto; align-items: center; gap: var(--space-2);
  min-height: 56px; padding-inline: clamp(.5rem, 2.2vw, 2rem); border-bottom: 1px solid var(--border); }
.conversation-heading { min-width: 0; color: var(--text); font-size: var(--font-size-sm); font-weight: 650; }
.conversation-heading span { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.chat-control, .chat-menu > summary { display: grid; place-items: center; width: 42px; height: 42px; padding: 0; color: var(--text-muted);
  border: 1px solid transparent; background: transparent; border-radius: var(--radius-md); }
.chat-control:hover, .chat-menu > summary:hover, .chat-menu[open] > summary, .history-toggle[aria-expanded="true"] {
  background: var(--surface-2); color: var(--text); border-color: var(--border); }
.chat-menu { position: relative; z-index: 2; }
.chat-menu > summary { list-style: none; }
.chat-menu > summary::-webkit-details-marker { display: none; }
.chat-menu-panel { position: absolute; inset: calc(100% + .45rem) 0 auto auto; display: grid; gap: var(--space-2);
  width: min(250px, 76vw); padding: var(--space-3); background: var(--sheet); border: 1px solid var(--border);
  border-radius: var(--radius-md); box-shadow: var(--elevation-2); }
.chat-menu-panel :is(a, button) { display: flex; align-items: center; min-height: 40px; padding-inline: var(--space-1); }
.chat-content { display: grid; grid-template-columns: minmax(0, 1fr); grid-template-rows: minmax(0, 1fr) auto; min-height: 0; }
.chat-content.is-start { display: block; overflow-y: auto; overscroll-behavior: contain; }
:global(:root[data-chat-width="narrow"] .workspace .conversation) { --chat-content-width: 610px; }
:global(:root[data-chat-width="wide"] .workspace .conversation) { --chat-content-width: 1100px; }
.chat-start, .chat-start-extra, .is-start .composer-wrap { width: min(calc(100% - 2rem), 742px); margin-inline: auto; }
.chat-start { padding-block-start: clamp(2.5rem, 10vh, 7rem); }
.chat-start-extra { padding-block-end: var(--space-5); }
.chat-start h1 { margin: 0 0 clamp(1.6rem, 4vw, 2.75rem); font: 400 clamp(2.65rem, 6.4vw, 5.5rem)/1 var(--font-serif); letter-spacing: -.055em; }
.chat-start h1 span { color: var(--accent); }
.start-choices { display: flex; flex-wrap: wrap; gap: var(--space-2); padding-top: var(--space-4); }
.start-choices button { min-height: 40px; font-weight: 500; }
:global(:root[data-preset="studio"] .start-choices button) {
  --idea-color: var(--studio-olive);
  --idea-wash: var(--studio-olive-wash);
  color: var(--idea-color); background: var(--idea-wash); border-color: color-mix(in srgb, var(--idea-color) 70%, var(--idea-wash));
}
:global(:root[data-preset="studio"] .start-choices button:nth-child(even)) {
  --idea-color: var(--studio-plum);
  --idea-wash: var(--studio-plum-wash);
}
:global(:root[data-preset="studio"] .start-choices button::before) {
  content: ''; width: .5rem; height: .5rem; flex: 0 0 auto; border-radius: var(--radius-full); background: currentColor;
}
:global(:root[data-preset="studio"] .start-choices button:hover) {
  background: color-mix(in srgb, var(--idea-wash), var(--idea-color) 6%);
}
.transcript { min-height: 0; min-width: 0; overflow: auto; overscroll-behavior-y: contain;
  padding: clamp(1rem, 3.75vh, 3rem) max(var(--space-4), calc((100% - var(--chat-content-width)) / 2)) var(--space-5); }
.reading-heading { margin-block-end: var(--space-5); padding-block-end: var(--space-4); border-bottom: 1px solid var(--border); }
.reading-heading h1 { font: 400 clamp(2rem, 4vw, 3.55rem)/1.08 var(--font-serif); letter-spacing: -.045em; }
.chat-scope-name { display: flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-2); color: var(--accent);
  font-size: var(--font-size-sm); font-weight: 650; }
.chat-scope-name svg { width: 15px; height: 15px; color: color-mix(in srgb, var(--project-mark), var(--text) 65%); }
.status-line { display: flex; align-items: center; gap: var(--space-1); margin-top: var(--space-2); color: var(--text-muted); font-size: var(--font-size-xs); }
.status-line span { width: 6px; height: 6px; border: 1px solid currentColor; border-radius: 50%; }
.status-line .online { background: var(--success); border-color: var(--success); }
.project-overview { margin-top: var(--space-3); }
.project-overview-heading { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: var(--space-3);
  padding-block-end: var(--space-3); border-bottom: 1px solid var(--border); }
.project-conversations a { border: 1px solid var(--border); background: var(--surface-1); padding: var(--space-3); }
.project-conversations a:hover { background: var(--surface-2); }
.message { display: grid; grid-template-columns: minmax(0, 1fr); min-width: 0; gap: var(--space-2);
  animation: message-in var(--motion-base) var(--ease-out) both; }
.message + .message { margin-top: var(--space-5); }
.message.user { width: fit-content; max-width: min(100%, 600px); padding: var(--space-2) var(--space-3);
  background: var(--message-user-bg); border-radius: var(--radius-lg) var(--radius-lg) var(--radius-lg) var(--radius-xs); }
.message-meta { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); color: var(--text-muted); font-size: var(--font-size-sm); }
.message-meta time { font-size: var(--font-size-xs); }
.message.user .message-meta { font-size: var(--font-size-xs); }
.message.user .message-meta .ui-avatar { display: none; }
.message.assistant .message-body { font-size: var(--font-size-lg); line-height: 1.6; }
.message-body { min-width: 0; line-height: 1.6; overflow-wrap: anywhere; }
.message.assistant :deep(.markdown :is(p, li)) { line-height: inherit; }
.message.assistant :deep(.markdown li + li) { margin-top: .35em; }
.message.assistant :deep(.markdown :is(h1, h2, h3, h4, h5, h6)) {
  font: 600 1em/1.18 var(--font-serif); letter-spacing: -.025em; margin-block: .8em .42em; }
.message.assistant :deep(.markdown > :first-child) { margin-block-start: 0; }
.message.assistant :deep(.markdown h1) { font-size: 1.6em; }
.message.assistant :deep(.markdown h2) { font-size: 1.45em; }
.message.assistant :deep(.markdown h3) { font-size: 1.25em; }
.message.assistant :deep(.markdown blockquote) { border-inline-start-color: var(--accent); color: var(--text-muted); }
.message.assistant :deep(.code-frame) { font-size: var(--font-size-sm); }
@keyframes message-in { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: translateY(0); } }
:global(:root[data-message-style="cards"] .workspace .message) { border: 1px solid var(--border); padding: var(--space-3); border-radius: var(--radius-md); }
:global(:root[data-message-style="bubbles"] .workspace .message) { width: fit-content; max-width: 94%; border-radius: var(--radius-xl); padding: var(--space-3); }
:global(:root[data-message-style="bubbles"] .workspace .message.assistant) { background: var(--message-surface); border: 1px solid var(--border); }
:global(:root[data-user-side="right"] .workspace .message.user) { margin-inline-start: auto; border-end-end-radius: var(--radius-xs); }
.message-actions, .suggestions, .feedback-reasons { display: flex; align-items: center; flex-wrap: wrap; gap: var(--space-1); }
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
.composer-wrap { position: relative; display: grid; gap: var(--space-2);
  padding: var(--space-2) max(var(--space-3), calc((100% - var(--chat-content-width)) / 2)) max(var(--space-3), env(safe-area-inset-bottom, 0px));
  border-top: 1px solid var(--border); }
.is-start .composer-wrap { padding: 0; border: 0; }
.is-start .composer { grid-template-columns: 42px minmax(0, 1fr) auto; padding: var(--space-3); }
.is-start .composer textarea { grid-area: 1 / 1 / 2 / -1; margin-bottom: var(--space-4); }
.is-start .composer-action { grid-area: 2 / 3; display: inline-flex; width: auto; padding-inline: var(--space-3); }
.is-start .composer-attach { grid-area: 2 / 1; }
.jump-latest { position: absolute; right: var(--space-4); bottom: calc(100% + var(--space-2)); z-index: 1;
  background: var(--surface-1); box-shadow: var(--elevation-2); }
.composer { position: relative; transition: box-shadow var(--motion-base), border-color var(--motion-base); }
.composer:focus-within, .composer.dragging { border-color: var(--accent); box-shadow: var(--glow-sm); }
.composer:has(textarea:focus-visible) { outline: 2px solid var(--focus); outline-offset: 1px; }
.composer .send-button { background-image: linear-gradient(145deg, var(--btn-primary-bg), var(--contrast-fill)); }
.send-button:not(:disabled):active { transform: scale(.94); }
.drop-label { position: absolute; inset: -1px; z-index: 1; display: grid; place-items: center; padding: var(--space-2);
  pointer-events: none; background: var(--surface-3); border: 2px dashed var(--accent); border-radius: inherit; color: var(--text); }
.composer-count { grid-column: 1 / -1; justify-self: end; color: var(--text-muted); }
.file-list.picked li { max-width: 100%; }
.file-list.picked small { white-space: nowrap; color: var(--text-muted); }
.composer { display: grid; grid-template-columns: 42px minmax(0, 1fr) 42px; gap: var(--space-1); align-items: end;
  border: 1px solid var(--composer-border); border-radius: var(--radius-lg); padding: 5px;
  background: var(--input); box-shadow: var(--elevation-1); }
.composer.text-only { grid-template-columns: minmax(0, 1fr) 42px; }
.composer textarea { padding: 8px 4px; line-height: 21px; border: 0; resize: none; min-height: 37px; max-height: 142px;
  overflow-y: auto; background: transparent; font-size: max(16px, 1rem); }
.composer textarea:focus-visible { outline: none; }
.composer-attach, .composer-action { display: grid; place-items: center; width: 42px; height: 42px; min-height: 42px; padding: 0; border-radius: var(--radius-md); }
.composer-attach { color: var(--text-muted); border: 0; background: transparent; }
.composer-attach:hover { color: var(--text); background: var(--surface-2); }
.stop-button, .stop-button:hover:not(:disabled) { background: var(--danger-fill); border-color: var(--danger-fill); color: white; }
.adaptation { display: flex; flex-wrap: wrap; align-items: baseline; gap: var(--space-2); }
@media (max-width: 900px) {
  .workspace { height: calc(var(--chat-viewport-height, 100dvh) - var(--header)); }
  .workspace.history-open { grid-template-columns: minmax(0, 1fr); }
  .history-tools { padding-block-start: var(--space-3); }
  .chat-control, .history-project-add { min-width: 40px; min-height: 40px; }
  .history :deep(.ui-icon-button) { min-width: 40px; min-height: 40px; }
  .message.assistant .message-body { font-size: var(--font-size-md); }
}
@media (max-width: 480px) {
  .conversation-header { min-height: 48px; }
  .chat-start { padding-block-start: clamp(1.5rem, 8vh, 4rem); }
  .start-break { display: none; }
}
</style>
