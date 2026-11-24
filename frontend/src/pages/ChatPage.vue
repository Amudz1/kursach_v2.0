<script setup lang="ts">
import { computed, effectScope, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { explain } from '../api'
import { ArrowDown, ArrowRight, BookOpen, Bookmark, Check, ChevronDown, Code, Copy, Ellipsis, FileImage, FileText, Folder, Lightbulb, ListChecks, MessagesSquare, PanelLeft, Paperclip, PenLine, Pin, PinOff, Plus, RotateCcw, Search, Send, Settings, SlidersHorizontal, Square, ThumbsDown, ThumbsUp, X } from 'lucide-vue-next'
import { categoryColors, entityColor, entityIcon } from '../appearance/entities'
import { appearance } from '../appearance'
import { attachmentExtensions } from '../chat-attachments'
import { insertAtCaret, messageLimit, type DraftCaret } from '../chat-drafts'
import { quotedRequest, quotedSelection, quoteLimit, submittedLength } from '../chat-quotes'
import { focusHistoryTarget } from '../focus-history-target'
import { useSession } from '../session'
import ChatMarkdown from '../components/ChatMarkdown.vue'
import ChatNavigationPanel from '../components/ChatNavigationPanel.vue'
import ChatRulesPanel from '../components/ChatRulesPanel.vue'
import ProjectEditor from '../components/ProjectEditor.vue'
import UiAvatar from '../components/ui/UiAvatar.vue'
import UiButton from '../components/ui/UiButton.vue'
import UiChip from '../components/ui/UiChip.vue'
import UiIconButton from '../components/ui/UiIconButton.vue'
import UiDialog from '../components/ui/UiDialog.vue'
import { lockScroll, trapTab, unlockScroll } from '../components/ui/focus'
import { useToast } from '../components/ui/use-toast'
import type { ChatState } from '../use-chat'
import type { Chat, Message } from '../types'
import { Mic, Pause, Play, Quote, Volume2 } from 'lucide-vue-next'
import type { AnswerSpeech } from '../use-answer-speech'
import type { LoadVoiceSession, VoiceSession } from '../voice-session'

const props = defineProps<{
  chat: ChatState; voice?: VoiceSession; loadVoice: LoadVoiceSession; confirmVoiceDeparture: () => Promise<boolean>; focus?: boolean
}>()
const voicePhase = computed(() => props.voice?.recorder.phase.value ?? 'idle')
const voiceOpening = ref(false)
const voiceStatus = ref('')
const toast = useToast()
const {
  chats, messages, selected, selectedId, model, currentPlan, search, nextBefore, nextAfter, loadingPage, highlightedId, bookmarkPending,
  chatsHaveMore, draft, draftQuote, draftRestored, draftHadFiles, draftConflict, files, rejectedFiles, fileErrors,
  busy, loading, error, messageNavigationError, linked, flow, lastAnswer, adaptation, transcript, bindTranscript,
  workspaces, workspaceId, workspaceQuery, currentWorkspace,
  projectChats, projectHasMore, projectLoading, loadProjectChats, loadWorkspaces, openProject, projectRemoved,
  loadChats, loadMessages, openMessage, returnToConversation, toggleBookmark, newChat, patch, move, remove, changeDraft, chooseFiles, removeFile, send, stop, regenerate, rate,
  decide, download, resume, resumeRoute, rememberPosition, personas,
  clearDraft, keepOwnDraft, useExternalDraft, setDraftQuote,
} = props.chat
const speech = shallowRef<AnswerSpeech>()
const speechScope = effectScope(true)
const spokenId = computed(() => speech.value?.activeId.value)
const speechPaused = computed(() => speech.value?.paused.value)
const speechNotice = computed(() => speech.value?.notice.value)
let speechIntent = 0
let pageActive = true
const microphoneInUse = computed(() => voicePhase.value === 'requesting' || voicePhase.value === 'recording' || voicePhase.value === 'finishing')
watch(microphoneInUse, (active) => { if (active) stopSpeech() }, { flush: 'sync' })
const compactLayout = window.matchMedia('(max-width: 900px)')
const compact = ref(compactLayout.matches)
const coarsePointer = window.matchMedia('(pointer: coarse)')
const touchPointer = ref(coarsePointer.matches)
const sendWithEnter = computed(() => !compact.value && !touchPointer.value)
function updatePointer(event: MediaQueryListEvent) { touchPointer.value = event.matches }
const desktopHistory = ref(true)
const mobileHistory = ref(false)
const historyDialog = ref<HTMLDialogElement>()
const historyPanel = ref<HTMLElement>()
const historyToggle = ref<HTMLButtonElement>()
const historyVisible = computed(() => compact.value ? mobileHistory.value : desktopHistory.value)
const historyAction = computed(() => historyVisible.value ? 'Скрыть список диалогов' : 'Показать список диалогов')
const historyKey = props.focus ? 'budz.history.focus' : 'budz.history'
let historyScrollLocked = false
const historyActions = ref<string>()
try { desktopHistory.value = localStorage.getItem(historyKey) !== 'hidden' } catch { /* Use the default layout. */ }
const picker = ref<HTMLInputElement>()
const dialog = ref<HTMLDialogElement>()
const operation = ref<'rename' | 'delete' | 'move'>('rename')
const dialogChatId = ref<string>()
const modalText = ref('')
const moveDestination = ref('')
const moveOrigin = ref('')
const movePending = ref(false)
const moveError = ref('')
let moveTrigger: HTMLElement | undefined
const moveBlocked = computed(() => dialogChatId.value === selectedId.value && Boolean(busy.value || flow.value))
const copied = ref('')
let copyTimer: number | undefined
const feedbackTarget = ref('')
const messageMore = ref('')
const navigationAlert = ref<HTMLElement>()
const composer = ref<HTMLTextAreaElement>()
let voiceCaret: DraftCaret | undefined
const quoteOpen = ref(false)
const quoteId = ref('')
const quoteText = ref('')
const unavailableQuoteId = ref('')
let selectedQuoteText = ''
let selectedQuoteId = ''
const chatMenu = ref<HTMLDetailsElement>()
const navigationPanel = ref<InstanceType<typeof ChatNavigationPanel>>()
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
watch(() => session.account?.id, stopSpeech, { flush: 'sync' })
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
const draftLength = computed(() => submittedLength(quotedRequest(draft.value, draftQuote.value)))
const canSubmitDraft = computed(() => Boolean(draft.value.trim()) || (!draftQuote.value && !temporary.value && files.value.length > 0))
const defaultIdeas = ['Разобрать сложную тему', 'Составить план', 'Поработать над текстом']
const focusIdeas = [
  { label: 'Объяснить тему', icon: BookOpen, text: 'Объясни простыми словами тему: ' },
  { label: 'Составить план', icon: ListChecks, text: 'Помоги составить пошаговый план для задачи: ' },
  { label: 'Улучшить текст', icon: PenLine, text: 'Предложи, как сделать этот текст яснее: ' },
  { label: 'Разобрать код', icon: Code, text: 'Объясни, как работает этот код, и укажи, что можно улучшить:\n' },
]
const projectHistoryOpen = ref(false)
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
const reasons = { too_long: 'Слишком длинно', too_complex: 'Слишком сложно', inaccurate: 'Неточно', wrong_language: 'Не тот язык' }
const failureMessages: Record<string, string> = {
  ai_timeout: 'Модель не успела ответить.', ai_empty: 'Модель вернула пустой ответ.',
  ai_credentials: 'Сервис модели отклонил ключ.', ai_rate_limit: 'Сервис модели ограничил запросы.',
  generation_interrupted: 'Генерация прервалась после перезапуска.', ai_incomplete: 'Поток ответа оборвался.',
  daily_token_limit: 'Для этого сообщения не хватает токенов. Сократите запрос или проверьте бюджет на странице тарифов.',
}
const dialogTitles = { rename: 'Переименовать диалог', delete: 'Удалить диалог?', move: 'Переместить в проект' }
const dialogActions = { rename: 'Сохранить', delete: 'Удалить', move: 'Переместить' }
const formTitle = computed(() => dialogTitles[operation.value])

function updateLatestButton() {
  const element = transcript.value
  showLatest.value = !!element && element.scrollHeight - element.clientHeight - element.scrollTop > (props.focus ? 24 : 220)
  rememberPosition()
}
async function jumpToLatest() {
  if (nextAfter.value) { await loadMessages(true); return }
  const element = transcript.value
  if (element) element.scrollTop = element.scrollHeight
}

function openChatSearch() {
  if (chatMenu.value) {
    chatMenu.value.open = false
    chatMenu.value.querySelector('summary')?.focus()
  }
  void navigationPanel.value?.show('search')
}

function focusNavigationAlert() {
  if (messageNavigationError.value) navigationAlert.value?.focus({ preventScroll: true })
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
function appendStartIdea(text: string) {
  const separator = !draft.value || draft.value.endsWith('\n\n') ? '' : draft.value.endsWith('\n') ? '\n' : '\n\n'
  const combined = draft.value + separator + text
  if (combined.length > messageLimit) { toast.warning('В черновике недостаточно места для подсказки.'); return }
  setComposer(combined)
  void nextTick(() => composer.value?.setSelectionRange(combined.length, combined.length))
}

async function recordVoice() {
  if (voiceOpening.value) return
  voiceStatus.value = ''
  stopSpeech()
  const chat = selectedId.value
  const workspace = workspaceId.value
  voiceCaret = { draft: draft.value, offset: composer.value?.selectionEnd ?? draft.value.length }
  voiceOpening.value = true
  try {
    const session = props.voice ?? await props.loadVoice()
    if (pageActive && chat === selectedId.value && workspace === workspaceId.value) void session?.recorder.start()
  } catch {
    if (pageActive) toast.error('Не удалось открыть запись голоса. Обновите страницу и повторите.')
  } finally { voiceOpening.value = false }
}
async function insertVoice() {
  const voice = props.voice
  if (!voice?.transcription.text.value.trim()) return
  const transcript = voice.transcription.text.value
  const insertion = insertAtCaret(draft.value, transcript, voiceCaret)
  if (insertion.draft.length > messageLimit || submittedLength(quotedRequest(insertion.draft, draftQuote.value)) > messageLimit) {
    voice.transcription.error.value = 'Черновик превысит 10 000 символов. Сократите расшифровку перед вставкой.'
    return
  }
  voice.reset()
  setComposer(insertion.draft)
  await nextTick()
  composer.value?.setSelectionRange(insertion.offset, insertion.offset)
  voiceStatus.value = 'Расшифровка добавлена в черновик'
}
async function sendDraft() {
  if (busy.value || flow.value || !canSubmitDraft.value) return
  if (!selectedId.value && !await props.confirmVoiceDeparture()) return
  await send()
}

function stopSpeech() {
  speechIntent++
  speech.value?.stop()
}
async function playAnswer(item: Message) {
  if (microphoneInUse.value) return
  const request = ++speechIntent
  const content = transcript.value?.querySelector<HTMLElement>(`#message-${item.id} .message-body .markdown`)
  if (!content) return
  try {
    if (!speech.value) {
      const module = await import('../use-answer-speech')
      if (!speechScope.active || request !== speechIntent) return
      speech.value = speechScope.run(module.useAnswerSpeech)
    }
    speech.value?.start(item.id, content, item.rules?.language)
  } catch { if (pageActive) toast.error('Не удалось загрузить озвучивание. Обновите страницу и повторите.') }
}

function captureQuoteSelection(item: Message) {
  const content = transcript.value?.querySelector<HTMLElement>(`#message-${item.id} .message-body .markdown`)
  selectedQuoteId = item.id
  selectedQuoteText = content ? quotedSelection(content, window.getSelection()) : ''
}
function startQuote(item: Message) {
  if (selectedQuoteId !== item.id || !selectedQuoteText) captureQuoteSelection(item)
  const fallback = transcript.value?.querySelector<HTMLElement>(`#message-${item.id} .message-body .markdown`)
  const first = fallback?.querySelector('p, li, h1, h2, h3, h4, pre')?.textContent?.trim() || item.content.trim()
  quoteText.value = selectedQuoteText || first.slice(0, quoteLimit).trimEnd()
  selectedQuoteId = selectedQuoteText = ''
  quoteId.value = item.id
  quoteOpen.value = true
}
function applyQuote() {
  const text = quoteText.value.trim()
  if (!text || text.length > quoteLimit) return
  setDraftQuote({ messageId: quoteId.value, text })
  unavailableQuoteId.value = ''
  quoteOpen.value = false
  void nextTick(() => composer.value?.focus())
}
async function followQuoteSource() {
  const quote = draftQuote.value
  if (!quote) return
  const found = await openMessage(quote.messageId)
  if (quote === draftQuote.value) unavailableQuoteId.value = found ? '' : quote.messageId
}

function onComposerKeydown(event: KeyboardEvent) {
  if (event.key !== 'Enter' || !sendWithEnter.value || event.isComposing || event.keyCode === 229) return
  if (event.shiftKey || event.ctrlKey || event.altKey || event.metaKey) return
  event.preventDefault()
  void sendDraft()
}
function showModelOptions() {
  if (!chatMenu.value) return
  chatMenu.value.open = true
  chatMenu.value.querySelector('select')?.focus({ preventScroll: true })
}

async function showDialog(type: typeof operation.value, target = selected.value, event?: MouseEvent) {
  if (!target) return
  if (type === 'move') {
    if (target.expiresAt) return
    const row = (event?.currentTarget as HTMLElement | undefined)?.closest('.thread-row')
    moveTrigger = mobileHistory.value ? historyToggle.value : row
      ? row.querySelector<HTMLElement>('.thread-actions-toggle') ?? undefined
      : chatMenu.value?.querySelector('summary') ?? undefined
    moveOrigin.value = moveDestination.value = target.workspaceId ?? ''
    moveError.value = ''
    historyActions.value = undefined
  }
  if (chatMenu.value) chatMenu.value.open = false
  closeMobileHistory()
  operation.value = type
  dialogChatId.value = target.id
  modalText.value = type === 'rename' ? target.title : ''
  await nextTick()
  dialog.value?.showModal()
}
function restoreMoveFocus() {
  moveTrigger?.focus()
  moveTrigger = undefined
}
async function applyDialog() {
  if (operation.value === 'move') {
    const id = dialogChatId.value
    const destination = moveDestination.value
    if (!id || movePending.value || moveBlocked.value || destination === moveOrigin.value) return
    movePending.value = true
    moveError.value = ''
    try {
      await move(id, destination)
      dialog.value?.close()
      toast.success(destination ? 'Диалог перемещён в проект' : 'Диалог перемещён в личные диалоги')
    } catch (reason) {
      moveError.value = explain(reason)
    } finally {
      movePending.value = false
    }
    return
  }
  if (operation.value === 'delete') { dialog.value?.close(); await remove(dialogChatId.value); return }
  const text = modalText.value.trim()
  if (text && await patch({ title: text }, dialogChatId.value)) dialog.value?.close()
}
async function copy(message: Message) {
  try {
    await navigator.clipboard.writeText(message.content)
    copied.value = message.id
    window.clearTimeout(copyTimer)
    copyTimer = window.setTimeout(() => { copied.value = '' }, 1400)
    if (message.role === 'user') toast.success('Скопировано')
  } catch {
    error.value = message.role === 'user' ? 'Не удалось скопировать сообщение.' : 'Не удалось скопировать ответ.'
  }
}
function addedFiles(event: Event) {
  const input = event.target as HTMLInputElement
  chooseFiles(input.files)
  input.value = ''
}
function setModel(event: Event) { model.value = (event.target as HTMLSelectElement).value }

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

function releaseHistoryScroll() {
  if (!historyScrollLocked) return
  historyScrollLocked = false
  unlockScroll()
}
function onHistoryClosed() {
  if (historyDialog.value?.open) return
  mobileHistory.value = false
  historyActions.value = undefined
  releaseHistoryScroll()
}
function trapHistoryTab(event: KeyboardEvent) {
  if (compact.value && historyPanel.value) trapTab(event, historyPanel.value)
}

function setHistory(visible: boolean) {
  if (compact.value) {
    if (visible) {
      historyDialog.value?.showModal()
      mobileHistory.value = !!historyDialog.value?.open
      if (mobileHistory.value && !historyScrollLocked) {
        lockScroll()
        historyScrollLocked = true
      }
    } else {
      historyDialog.value?.close()
      onHistoryClosed()
    }
    return
  }
  desktopHistory.value = visible
  if (!visible) historyActions.value = undefined
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
async function startNewChat() {
  closeMobileHistory()
  if (!await props.confirmVoiceDeparture()) return
  void newChat()
}
function resizeLayout(event: MediaQueryListEvent) {
  historyDialog.value?.close()
  onHistoryClosed()
  compact.value = event.matches
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
  if (!selectedId.value && composer.value === document.activeElement) {
    void nextTick(() => composer.value?.closest('form')?.scrollIntoView({ block: 'nearest' }))
  }
}

let sizeObserver: ResizeObserver | undefined
watch([draft, viewportHeight], resizeComposer, { flush: 'post' })
watch(selectedId, () => {
  stopSpeech()
  showLatest.value = false
  messageMore.value = ''
  quoteOpen.value = false
  selectedQuoteId = selectedQuoteText = ''
  unavailableQuoteId.value = ''
})
watch(workspaceId, () => { projectHistoryOpen.value = false })
watch(messageNavigationError, focusNavigationAlert, { flush: 'post' })
onMounted(() => {
  focusNavigationAlert()
  compactLayout.addEventListener('change', resizeLayout)
  coarsePointer.addEventListener('change', updatePointer)
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
  pageActive = false
  speechScope.stop()
  releaseHistoryScroll()
  window.clearTimeout(copyTimer)
  compactLayout.removeEventListener('change', resizeLayout)
  coarsePointer.removeEventListener('change', updatePointer)
  window.visualViewport?.removeEventListener('resize', resizeViewport)
  sizeObserver?.disconnect()
})
</script>

<template>
  <section class="workspace" :class="{ 'history-open': historyVisible }" :style="{ '--chat-viewport-height': viewportHeight }" aria-label="Диалоги Budz">
    <dialog id="mobile-chat-history" ref="historyDialog" class="mobile-history" aria-label="История диалогов"
      @close="onHistoryClosed" @click.self="hideHistory"></dialog>
    <Teleport to="#mobile-chat-history" :disabled="!compact || !mobileHistory" defer>
      <aside v-show="historyVisible" id="chat-history" ref="historyPanel" class="history" tabindex="-1" aria-label="Боковая панель диалогов" @keydown.esc.stop.prevent="hideHistory" @keydown.tab="trapHistoryTab">
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
            <div v-for="chat in group.items" :key="chat.id" class="thread-row" :class="{ 'with-pin': !focus }">
              <RouterLink :to="chatRoute(chat)" :title="focus ? chat.title : undefined" @click="closeMobileHistory">
                <span class="persona-dot" :style="{ '--history-entity-color': `var(--${personaColor(chat.personaId)})` }" aria-hidden="true"></span>
                <span class="thread-title"><span>{{ chat.title }}</span><small v-if="chat.workspaceId && projectNames.has(chat.workspaceId)">{{ projectNames.get(chat.workspaceId) }}</small></span>
              </RouterLink>
              <button v-if="focus || workspaces.length" type="button" class="chat-control thread-actions-toggle"
                :aria-label="`Действия диалога ${chat.title}`" :aria-expanded="historyActions === chat.id"
                :aria-controls="historyActions === chat.id ? `thread-actions-${chat.id}` : undefined"
                @click="historyActions = historyActions === chat.id ? undefined : chat.id">
                <Ellipsis aria-hidden="true" />
              </button>
              <UiIconButton v-if="!focus" class="thread-pin" :icon="chat.isPinned ? PinOff : Pin" :label="chat.isPinned ? 'Открепить диалог' : 'Закрепить диалог'"
                size="sm" @click="patch({ isPinned: !chat.isPinned }, chat.id)" />
              <div v-if="historyActions === chat.id" :id="`thread-actions-${chat.id}`" class="thread-actions">
                <button v-if="focus" type="button" @click="showDialog('rename', chat)">Переименовать</button>
                <button v-if="focus" type="button" @click="patch({ isPinned: !chat.isPinned }, chat.id)">{{ chat.isPinned ? 'Открепить' : 'Закрепить' }}</button>
                <button v-if="workspaces.length" type="button" aria-haspopup="dialog" @click="showDialog('move', chat, $event)">Переместить в проект</button>
                <button v-if="focus" type="button" class="danger-text" @click="showDialog('delete', chat)">Удалить</button>
              </div>
            </div>
          </section>
          <p v-if="!chats.length" class="muted">Диалогов пока нет.</p>
        </nav>
        <button v-if="chatsHaveMore" class="plain-button" @click="loadChats(false)">Ещё диалоги</button>
      </aside>
    </Teleport>

    <div class="conversation">
      <header class="conversation-header">
        <Teleport :to="focusHistoryTarget ?? undefined" :disabled="!focus || !focusHistoryTarget">
          <button ref="historyToggle" type="button" class="chat-control history-toggle" :aria-label="historyAction" :title="historyAction"
            :aria-expanded="historyVisible" :aria-haspopup="compact ? 'dialog' : undefined"
            :aria-controls="compact ? 'mobile-chat-history' : 'chat-history'" @click="toggleHistory">
            <PanelLeft aria-hidden="true" />
          </button>
        </Teleport>
        <div v-if="focus && selectedId" class="conversation-heading focus-conversation-heading">
          <p v-if="temporary" class="chat-scope-name">Временный диалог</p>
          <p v-else-if="currentWorkspace" class="chat-scope-name">
            <component :is="projectMark?.icon" :style="{ '--project-mark': projectMark?.color }" aria-hidden="true" />{{ currentWorkspace.name }}
          </p>
          <h1 :title="selected?.title">{{ selected?.title ?? 'Диалог' }}</h1>
          <p class="status-line"><span :class="{ online: linked }"></span>{{ linked ? 'Подключено' : 'Подключаемся к чату' }}</p>
        </div>
        <div v-else class="conversation-heading">
          <span>{{ selected?.title ?? currentWorkspace?.name ?? 'Диалоги' }}</span>
        </div>
        <div class="conversation-tools">
        <button v-if="selectedId" type="button" class="chat-control" aria-label="Оглавление" title="Оглавление"
          aria-haspopup="dialog" @click="navigationPanel?.show('outline')"><BookOpen aria-hidden="true" /></button>
        <details ref="chatMenu" class="chat-menu">
          <summary :aria-label="focus ? 'Действия диалога' : 'Настройки диалога'"><Ellipsis aria-hidden="true" /></summary>
          <div class="chat-menu-panel">
            <button type="button" class="text-button" aria-haspopup="dialog" @click="showRules">Правила</button>
            <button v-if="selectedId" type="button" class="text-button" aria-haspopup="dialog" @click="openChatSearch">Поиск по беседе</button>
            <label>Модель <select :value="model" aria-label="Модель" @change="setModel">
              <option value="">Автоматически</option><option v-for="allowed in currentPlan?.allowedModels" :key="allowed" :value="allowed">{{ allowed }}</option>
            </select></label>
            <button v-if="currentWorkspace && selectedId" class="text-button" @click="editProject">Настройки проекта</button>
            <template v-if="selectedId">
              <button v-if="!temporary && workspaces.length" type="button" class="text-button" aria-haspopup="dialog"
                @click="showDialog('move', selected, $event)">Переместить в проект</button>
              <RouterLink v-if="!temporary" class="text-button" :to="{ path: '/data', query: { chat: selectedId, ...workspaceQuery } }">Экспорт диалога</RouterLink>
              <button v-if="!temporary" class="text-button" @click="patch({ isPinned: !selected?.isPinned })">{{ selected?.isPinned ? 'Открепить' : 'Закрепить' }}</button>
              <button class="text-button" @click="showDialog('rename')">Переименовать</button>
              <button class="text-button danger-text" @click="showDialog('delete')">Удалить</button>
            </template>
          </div>
        </details>
        </div>
      </header>

      <div class="chat-content" :class="{ 'is-start': !selectedId }">
        <div v-if="!selectedId" class="chat-start">
          <template v-if="focus">
            <p v-if="currentWorkspace" class="chat-scope-name">
              <component :is="projectMark?.icon" :style="{ '--project-mark': projectMark?.color }" aria-hidden="true" />Проект
            </p>
            <h1 v-if="currentWorkspace">{{ currentWorkspace.name }}</h1>
            <h1 v-else>С чего начнём<span>?</span></h1>
          </template>
          <p v-if="!focus && currentWorkspace" class="chat-scope-name">
            <component :is="projectMark?.icon" :style="{ '--project-mark': projectMark?.color }" aria-hidden="true" />{{ currentWorkspace.name }}
          </p>
          <h1 v-if="!focus">Что хотите<br class="start-break"> разобрать<span>?</span></h1>
        </div>
        <div v-show="selectedId" :ref="bindTranscript" class="transcript" :role="focus ? 'region' : undefined"
          :tabindex="focus ? 0 : undefined" aria-label="Сообщения" @scroll.passive="updateLatestButton">
          <header v-if="selectedId && !focus" class="reading-heading">
            <p v-if="selected?.expiresAt" class="chat-scope-name" :title="new Date(selected.expiresAt).toLocaleString('ru-RU')">Временный диалог</p>
            <p v-else-if="currentWorkspace" class="chat-scope-name">
              <component :is="projectMark?.icon" :style="{ '--project-mark': projectMark?.color }" aria-hidden="true" />{{ currentWorkspace.name }}
            </p>
            <h1>{{ selected?.title ?? 'Диалог' }}</h1>
            <p class="status-line"><span :class="{ online: linked }"></span>{{ linked ? 'Подключено' : 'Подключаемся к чату' }}</p>
          </header>
          <p v-if="loading && !messages.length" class="muted" role="status">Загружаем сообщения…</p>
          <button v-if="nextBefore" class="plain-button" :disabled="loadingPage" @click="loadMessages(false, 'before')">Загрузить ранние сообщения</button>
          <div v-if="selectedId && !loading && !messages.length" class="start-choices" aria-label="Примеры запросов">
            <button v-for="idea in defaultIdeas" :key="idea" type="button" class="secondary" @click="setComposer(idea)">{{ idea }}</button>
          </div>
          <article v-for="item in messages" :id="`message-${item.id}`" :key="item.id" class="message message-frame"
            :class="[item.role, { 'message-target': highlightedId === item.id, 'actions-expanded': messageMore === item.id }]" tabindex="-1"
            :data-reading-message="item.status === 'pending' || item.status === 'streaming' ? undefined : item.id">
            <div class="message-meta">
              <UiAvatar v-if="item.role === 'user'" :name="session.account?.username ?? 'Вы'" size="sm" decorative />
              <UiAvatar v-else :name="activePersona?.name ?? 'Budz'" :brand="appearance.assistantAvatar !== 'persona'"
                :icon="assistantIcon" :color="personaColor(activePersona?.id)" size="sm" decorative />
              <strong>{{ item.role === 'user' ? 'Вы' : 'Budz' }}</strong>
              <time :datetime="item.createdAt">{{ time(item.createdAt) }}</time>
              <UiIconButton v-if="item.role === 'user' && item.content && item.status !== 'pending' && item.status !== 'streaming'"
                class="message-copy" size="sm" :icon="copied === item.id ? Check : Copy"
                label="Копировать сообщение" @click="copy(item)" />
              <UiIconButton v-if="selected && !temporary && item.role === 'assistant' && item.content &&
                item.status !== 'pending' && item.status !== 'streaming'" class="message-bookmark" size="sm" :icon="Bookmark"
                :disabled="bookmarkPending.has(item.id)" :pressed="!!item.isBookmarked"
                :label="item.isBookmarked ? 'Убрать закладку' : 'Добавить закладку'" @click="toggleBookmark(item)">
                <span v-if="item.isBookmarked" class="bookmark-dot" aria-hidden="true"></span>
                <Bookmark v-else class="bookmark-icon" aria-hidden="true" />
              </UiIconButton>
            </div>
            <div class="message-body">
              <ChatMarkdown v-if="item.content" :content="item.content" :answer="item.role === 'assistant'"
                :class="{ streaming: item.status === 'streaming' }" />
              <p v-if="!item.content && (item.status === 'pending' || item.status === 'streaming')" class="typing muted" role="status">
                Budz печатает <span aria-hidden="true"><i></i><i></i><i></i></span>
              </p>
              <p v-if="item.status === 'failed'" class="muted" role="status">{{ failureMessages[item.errorCode ?? ''] ?? 'Ответ недоступен.' }} Можно повторить генерацию.</p>
              <p v-if="item.status === 'stopped'" class="muted" role="status">Генерация остановлена.</p>
              <ul v-if="item.attachments?.length" class="file-list">
                <li v-for="attachment in item.attachments" :key="attachment.id"><button class="text-button" @click="download(attachment)">Скачать {{ attachment.fileName }}</button></li>
              </ul>
            </div>
            <div v-if="item.role === 'assistant' && item.status !== 'pending' && item.status !== 'streaming'" class="message-actions">
              <UiIconButton v-if="!focus && item.rules" :icon="ListChecks" label="Правила ответа" @click="rulesPanel?.show(item.rules)" />
              <UiIconButton v-if="item.content" :icon="copied === item.id ? Check : Copy" :label="copied === item.id ? 'Скопировано' : 'Копировать'" @click="copy(item)" />
              <button v-if="item.status === 'completed' && item.content" type="button" class="chat-control message-quote"
                aria-label="Цитировать" title="Цитировать" aria-haspopup="dialog"
                @pointerdown="captureQuoteSelection(item)" @click="startQuote(item)"><Quote aria-hidden="true" /></button>
              <UiIconButton v-if="item.id === lastAnswer?.id && !flow" :icon="RotateCcw" label="Повторить" :disabled="busy" @click="regenerate(item)" />
              <template v-if="item.status === 'completed' && item.content">
                <UiIconButton v-if="spokenId !== item.id" :icon="Volume2" label="Озвучить" :disabled="microphoneInUse" @click="playAnswer(item)" />
                <template v-else>
                  <UiIconButton :icon="speechPaused ? Play : Pause" :label="speechPaused ? 'Продолжить' : 'Пауза'" @click="speech?.togglePause()" />
                  <UiIconButton :icon="Square" label="Остановить озвучивание" @click="stopSpeech" />
                </template>
              </template>
              <button v-if="focus && (item.rules || (!temporary && item.status === 'completed'))" type="button" class="chat-control message-more"
                aria-label="Ещё" :aria-expanded="messageMore === item.id"
                @click="messageMore = messageMore === item.id ? '' : item.id">
                <Ellipsis aria-hidden="true" />
              </button>
              <UiIconButton v-if="focus && item.rules" class="answer-secondary" :icon="ListChecks" label="Правила ответа" @click="rulesPanel?.show(item.rules)" />
              <template v-if="!temporary && item.status === 'completed'">
                <UiIconButton class="answer-secondary" :icon="ThumbsUp" label="Полезно" :pressed="item.rating === 1" @click="rate(item, 1)" />
                <UiIconButton class="answer-secondary" :icon="ThumbsDown" label="Не помогло" :pressed="item.rating === -1" @click="feedbackTarget = feedbackTarget === item.id ? '' : item.id" />
              </template>
            </div>
            <p v-if="speechNotice?.id === item.id" class="muted" role="status">
              {{ speechNotice.message }}
            </p>
            <div v-if="feedbackTarget === item.id" class="feedback-reasons" aria-label="Причина оценки">
              <button v-for="(label, code) in reasons" :key="code" class="secondary" @click="rate(item, -1, code); feedbackTarget = ''">{{ label }}</button>
              <button class="text-button" @click="rate(item, -1); feedbackTarget = ''">Без причины</button>
            </div>
            <div v-if="item.id === lastAnswer?.id && item.suggestions.length" class="suggestions" aria-label="Вопросы для продолжения">
                <UiChip v-for="(idea, index) in item.suggestions" :key="idea" :category="suggestionColors[index % suggestionColors.length]"
                :icon="Lightbulb" arrow @click="setComposer(idea)">{{ idea }}</UiChip>
            </div>
          </article>
          <button v-if="nextAfter" class="plain-button newer-messages" :disabled="loadingPage" @click="loadMessages(false, 'after')">Загрузить более новые сообщения</button>
        </div>

        <div class="composer-wrap">
          <div v-if="messageNavigationError" ref="navigationAlert" class="notice danger navigation-alert" role="alert" tabindex="-1">
            <p>{{ messageNavigationError }}</p>
            <button type="button" class="text-button" @click="returnToConversation">Вернуться к беседе</button>
            <button type="button" class="text-button" @click="navigationPanel?.show('search', true)">Повторить поиск</button>
          </div>
          <UiButton v-if="selectedId && (showLatest || nextAfter)" class="jump-latest" size="sm" :icon="ArrowDown"
            :disabled="loadingPage" @click="jumpToLatest">К последнему сообщению</UiButton>
          <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
          <div v-if="adaptation && !temporary" class="notice adaptation" role="status">
            <span>Последние оценки предлагают сделать ответы короче.</span>
            <button class="text-button" @click="decide(true)">Применить</button>
            <button class="text-button" @click="decide(false)">Скрыть</button>
          </div>
          <form class="composer" :class="{ 'text-only': temporary, dragging: dragDepth > 0 }" @submit.prevent="sendDraft"
            @dragenter="dragEnter" @dragover="dragOver" @dragleave="dragDepth = Math.max(0, dragDepth - 1)" @drop.prevent="dropFiles">
            <div v-if="draftRestored || draftHadFiles || draftConflict" class="draft-status">
              <p v-if="draftRestored" class="muted" role="status">Черновик восстановлен</p>
              <p v-if="draftHadFiles" class="muted">Прикрепите ранее выбранные файлы повторно.</p>
              <div v-if="draftConflict" class="draft-conflict">
                <p role="status">В другой вкладке изменилась версия черновика.</p>
                <button type="button" class="text-button" :disabled="busy" @click="useExternalDraft">Загрузить версию из другой вкладки</button>
                <button type="button" class="text-button" :disabled="busy" @click="keepOwnDraft">Оставить мою версию</button>
              </div>
            </div>
            <span v-if="dragDepth > 0" class="drop-label" role="status">Отпустите, чтобы прикрепить</span>
            <input v-if="!temporary" ref="picker" class="sr-only" type="file" tabindex="-1" aria-label="Выбор файла" multiple :accept="attachmentExtensions.join(',')" @change="addedFiles">
            <button v-if="!focus && !temporary" type="button" class="composer-attach" aria-label="Прикрепить" title="Прикрепить" :disabled="busy" @click="picker?.click()"><Paperclip aria-hidden="true" /></button>
            <label class="sr-only" for="composer">Сообщение для Budz</label>
            <textarea id="composer" ref="composer" :value="draft" rows="1" :maxlength="messageLimit"
              :placeholder="selectedId ? 'Продолжите мысль…' : focus ? 'Напишите вопрос или запишите голос…' : 'Напишите вопрос или мысль…'"
              :enterkeyhint="sendWithEnter ? 'send' : 'enter'"
              @input="changeDraft(($event.target as HTMLTextAreaElement).value)" @keydown="onComposerKeydown"></textarea>
            <div class="composer-actions">
              <div v-if="focus" class="composer-tools">
                <button v-if="!temporary" type="button" class="composer-attach" aria-label="Прикрепить" title="Прикрепить" :disabled="busy" @click="picker?.click()"><Paperclip aria-hidden="true" /></button>
                <button type="button" class="composer-attach" aria-label="Настройки модели" title="Настройки модели" @click="showModelOptions"><SlidersHorizontal aria-hidden="true" /></button>
              </div>
              <div class="composer-send-controls">
              <button v-if="voicePhase === 'idle'" type="button" class="composer-attach voice-trigger" aria-label="Записать голос"
                title="Записать голос" :aria-busy="voiceOpening || undefined" @click="recordVoice"><Mic aria-hidden="true" /></button>
              <button v-if="flow" type="button" class="primary composer-action stop-button" aria-label="Остановить" title="Остановить" @click="stop"><Square aria-hidden="true" /></button>
              <button v-else type="submit" class="primary composer-action send-button" aria-label="Отправить" title="Отправить" :disabled="busy || !canSubmitDraft"><Send aria-hidden="true" /><span v-if="!selectedId">Отправить</span></button>
              </div>
            </div>
            <component :is="voice.panel" v-if="voice" :recorder="voice.recorder" :transcription="voice.transcription"
              @insert="insertVoice" />
            <span class="sr-only" role="status">{{ voiceStatus }}</span>
            <div v-if="draftQuote" class="draft-status draft-quote" role="group" aria-label="Цитата к сообщению">
              <blockquote>{{ draftQuote.text }}</blockquote>
              <div class="actions">
                <RouterLink :to="{ path: `/chats/${selectedId}`, query: { ...workspaceQuery, at: draftQuote.messageId } }" custom v-slot="{ href }">
                  <a :href="href" class="text-button" @click.prevent="followQuoteSource">К исходному ответу</a>
                </RouterLink>
                <button type="button" class="text-button" @click="setDraftQuote()">Убрать цитату</button>
              </div>
              <p v-if="unavailableQuoteId === draftQuote.messageId" class="muted" role="status">Источник недоступен. Текст цитаты сохранён.</p>
              <p v-if="!draft.trim()" class="muted">Добавьте вопрос или пояснение к цитате.</p>
            </div>
            <button v-if="draft || draftQuote || files.length || draftHadFiles" class="text-button clear-draft" type="button" :disabled="busy"
              @click="clearDraft">Очистить черновик</button>
            <small v-if="draftLength >= 8000" class="composer-count">{{ numberFormat.format(draftLength) }} / 10 000 символов · ≈ {{ numberFormat.format(Math.ceil(draftLength / 4)) }} токенов</small>
            <ul v-if="!temporary && files.length" class="file-list picked" aria-label="Приложенные файлы">
              <li v-for="(file, index) in files" :key="`${file.name}-${index}`">
                <component :is="file.type.startsWith('image/') ? FileImage : FileText" class="project-icon" aria-hidden="true" />
                <span class="picked-file"><span>{{ file.name }}</span><small>{{ formatFileSize(file.size) }}</small>
                  <small v-if="fileErrors.has(file)" class="picked-error" role="alert">{{ fileErrors.get(file) }}</small>
                </span>
                <UiIconButton :icon="X" size="sm" :label="`Удалить ${file.name}`" :disabled="busy" @click="removeFile(index)" />
              </li>
            </ul>
            <div v-if="!temporary && rejectedFiles.length" class="rejected-files" role="alert">
              <p v-for="(item, index) in rejectedFiles" :key="index"><strong>{{ item.name }}</strong> — {{ item.message }}</p>
            </div>
          </form>
        </div>
        <div v-if="!selectedId" class="chat-start-extra">
          <div v-if="focus" class="start-choices focus-start-ideas" aria-label="Примеры запросов">
            <UiButton v-for="idea in focusIdeas" :key="idea.label" variant="secondary" size="sm" :icon="idea.icon"
              @click="appendStartIdea(idea.text)">{{ idea.label }}</UiButton>
          </div>
          <div v-if="focus && currentWorkspace" class="focus-project-actions">
            <UiButton variant="ghost" size="sm" :icon="MessagesSquare" :aria-expanded="projectHistoryOpen"
              aria-controls="project-history" @click="projectHistoryOpen = !projectHistoryOpen">Диалоги проекта</UiButton>
            <UiButton variant="ghost" size="sm" :icon="Settings" @click="editProject">Настройки проекта</UiButton>
          </div>
          <RouterLink v-if="resume && resumeRoute" :to="resumeRoute" class="resume-chat">
            <span v-if="focus" class="focus-resume-icon"><Bookmark aria-hidden="true" /></span>
            <span v-else class="bookmark-dot" aria-hidden="true"></span>
            <span>
              <small>Продолжить</small>
              <strong>{{ resume.chat.title }}</strong>
              <span v-if="resume.chat.workspaceId && projectNames.has(resume.chat.workspaceId)" class="resume-project">
                {{ projectNames.get(resume.chat.workspaceId) }}
              </span>
              <span v-if="resume.text" class="resume-snippet">{{ resume.text }}</span>
            </span>
            <component :is="focus ? ArrowRight : BookOpen" aria-hidden="true" />
          </RouterLink>
          <section v-if="currentWorkspace" v-show="!focus || projectHistoryOpen" id="project-history" class="project-overview stack"
            aria-labelledby="project-conversations">
            <h2 v-if="focus" id="project-conversations" class="sr-only">Диалоги проекта</h2>
            <header v-else class="project-overview-heading">
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
          <div v-else-if="!focus && !resume" class="start-choices" aria-label="Примеры запросов">
            <button v-for="idea in defaultIdeas" :key="idea" type="button" class="secondary" @click="setComposer(idea)">{{ idea }}</button>
          </div>
        </div>
      </div>
    </div>

    <dialog ref="dialog" aria-labelledby="chat-dialog-title" @cancel="movePending && $event.preventDefault()" @close="restoreMoveFocus">
      <form class="stack" @submit.prevent="applyDialog">
        <h2 id="chat-dialog-title">{{ formTitle }}</h2>
        <p v-if="operation === 'delete'">Переписка и файлы этого диалога будут удалены.</p>
        <template v-else-if="operation === 'move'">
          <label>Проект назначения
            <select v-model="moveDestination" :disabled="movePending" autofocus @change="moveError = ''">
              <option value="">Личные диалоги</option>
              <option v-for="space in workspaces" :key="space.id" :value="space.id">{{ space.name }}</option>
            </select>
          </label>
          <p v-if="moveBlocked" class="muted" role="status">Дождитесь завершения ответа перед переносом.</p>
          <p v-if="moveError" class="notice danger" role="alert">{{ moveError }}</p>
        </template>
        <label v-else>Название <input v-model="modalText" maxlength="120" required autofocus></label>
        <div class="actions">
          <button type="button" class="secondary" :disabled="movePending" @click="dialog?.close()">Отмена</button>
          <button type="submit" :class="operation === 'delete' ? 'danger-button' : 'primary'"
            :disabled="operation === 'move' ? movePending || moveBlocked || moveDestination === moveOrigin : busy">
            {{ movePending ? 'Перемещаем…' : dialogActions[operation] }}
          </button>
        </div>
      </form>
    </dialog>
    <ChatRulesPanel ref="rulesPanel" :chat-id="selectedId" :workspace-id="workspaceId" :last="lastAnswer?.rules" />
    <ChatNavigationPanel ref="navigationPanel" :chat-id="selectedId" :temporary="temporary" @jump="openMessage" />
    <UiDialog v-model="quoteOpen" title="Цитировать ответ" size="sm">
      <form id="chat-quote-form" class="stack" @submit.prevent="applyQuote">
        <label>Фрагмент ответа <textarea v-model="quoteText" rows="4" autofocus /></label>
        <p class="muted" role="status">
          {{ quoteText.trim().length }} / {{ quoteLimit }} символов.
          <span v-if="quoteText.trim().length > quoteLimit">Сократите фрагмент перед добавлением.</span>
        </p>
      </form>
      <template #footer>
        <UiButton @click="quoteOpen = false">Отмена</UiButton>
        <UiButton variant="primary" type="submit" form="chat-quote-form" :disabled="!quoteText.trim() || quoteText.trim().length > quoteLimit">
          Добавить цитату
        </UiButton>
      </template>
    </UiDialog>
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
.thread-actions-toggle { margin-inline-end: 2px; }
.thread-row a[aria-current] .thread-title > * {
  overflow: visible; white-space: normal; overflow-wrap: anywhere;
}
.thread-actions {
  grid-column: 1 / -1; display: grid; gap: 1px; padding: var(--space-1);
  background: var(--sheet); border: 1px solid var(--border); border-radius: var(--radius-md);
}
.thread-actions button {
  min-height: 40px; padding-inline: var(--space-2); text-align: start;
  background: transparent; border: 0; border-radius: var(--radius-sm);
}
.thread-actions button:hover { background: var(--panel-active); }
.thread-row.with-pin:has(.thread-actions-toggle) { grid-template-columns: minmax(0, 1fr) auto auto; }
.thread-pin { justify-self: end; margin-inline-end: 2px; }
@media (hover: hover) {
  .thread-row:not(:hover, :focus-within) .thread-actions-toggle[aria-expanded="false"] { opacity: 0; }
  .thread-row:not(:hover, :focus-within) .thread-pin { opacity: 0; }
}
.persona-dot { width: 8px; height: 8px; background: currentColor; border-radius: var(--radius-full); flex: 0 0 auto; }
.project-icon, .history-project-add svg, .chat-control svg, .chat-menu > summary svg,
.composer-action svg, .composer-attach svg { width: 17px; height: 17px; flex: 0 0 auto; }
.mobile-history { inset: 0 auto 0 0; width: min(88vw, 330px); height: 100dvh; max-width: none; max-height: none;
  margin: 0; padding: 0; overflow: hidden; background: var(--panel-surface); border: 0; border-radius: 0; }
.mobile-history .history { height: 100%; }
.conversation { display: grid; grid-template-columns: minmax(0, 1fr); grid-template-rows: auto minmax(0, 1fr);
  min-width: 0; min-height: 0; --chat-content-width: 920px; --chat-gutter: var(--space-4); }
.conversation-header { display: grid; grid-template-columns: 42px minmax(0, 1fr) auto; align-items: center; gap: var(--space-2);
  min-height: 56px; padding-inline: clamp(.5rem, 2.2vw, 2rem); border-bottom: 1px solid var(--border); }
.conversation-header:not(:has(> .history-toggle)) { grid-template-columns: minmax(0, 1fr) auto; }
.conversation-tools { display: flex; align-items: center; gap: var(--space-1); }
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
:global(:root[data-chat-width="wide"] .workspace .conversation) { --chat-content-width: 1280px; }
.chat-start, .chat-start-extra, .is-start .composer-wrap {
  width: min(calc(100% - 2 * var(--chat-gutter)), var(--chat-content-width)); margin-inline: auto;
}
.chat-start { padding-block-start: clamp(2.5rem, 10vh, 7rem); }
.chat-start-extra { padding-block-end: var(--space-5); }
.resume-chat { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: var(--space-3); margin-block-start: var(--space-4);
  padding: var(--space-3) var(--space-4); color: var(--text); text-decoration: none; background: var(--surface-3);
  border: 1px solid var(--border); border-inline-start: 3px solid var(--studio-bookmark); border-radius: var(--radius-md); }
.resume-chat:hover { background: var(--surface-2); border-color: var(--studio-bookmark); }
.resume-chat > span:nth-child(2) { display: grid; gap: var(--space-1); min-width: 0; overflow-wrap: anywhere; }
.resume-chat small { color: var(--accent); font-weight: 700; }
.resume-project, .resume-snippet { color: var(--text-muted); font-size: var(--font-size-sm); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.resume-chat svg { width: 18px; height: 18px; color: var(--text-muted); }
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
  container: chat-transcript / inline-size;
  padding: clamp(1rem, 3.75vh, 3rem) max(var(--chat-gutter), calc((100% - var(--chat-content-width)) / 2)) var(--space-5); }
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
.message-target { outline: 2px solid var(--focus); outline-offset: -2px; }
.newer-messages { margin-block-start: var(--space-3); }
.message.user {
  --message-copy-size: 40px; width: fit-content; background: var(--message-user-bg);
  max-width: min(var(--user-message-limit, 600px), calc(100% - var(--message-copy-gutter, 0px)));
}
.message-meta { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); color: var(--text-muted); font-size: var(--font-size-sm); }
.message-bookmark { margin-inline-start: auto; }
.bookmark-dot { width: 9px; height: 9px; border-radius: 50%; background: var(--studio-bookmark); }
.bookmark-icon { width: 16px; height: 16px; }
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
:global(:root[data-message-style="bubbles"] .workspace .message.assistant) {
  width: fit-content; max-width: 94%; background: var(--assistant-bubble, var(--message-surface));
}
:global(:root[data-message-style="bubbles"] .workspace .message.user) { --user-message-limit: 94%; }
:global(:root[data-user-side="right"] .workspace .message.user) { margin-inline-start: auto; }
.message-meta .message-copy { flex: none; margin-inline-start: auto; }
.message-copy :deep(.ui-icon-button) { width: var(--message-copy-size); height: var(--message-copy-size); }
:global(:root[data-user-side="right"] .workspace .message-copy) { order: -1; margin-inline: 0 auto; }
:is(.message-copy, .message-bookmark) :deep(.tooltip) { inset-inline: auto 0; transform: translateY(4px); }
:is(.message-copy, .message-bookmark) :deep(.tooltip.open) { transform: translateY(0); }
:global(:root[data-user-side="right"] .workspace .message-copy .tooltip) { inset-inline: 0 auto; }
@container chat-transcript (min-width: 640px) {
  .message.user:has(.message-copy) { --message-copy-gutter: var(--message-copy-size); }
  .message-meta .message-copy { position: absolute; inset-block-start: var(--space-1); inset-inline-start: 100%; margin-inline: 0; }
  :global(:root[data-user-side="right"] .workspace .message-copy) { inset-inline: auto 100%; margin-inline: 0; }
}
.message-actions, .suggestions, .feedback-reasons { display: flex; align-items: center; flex-wrap: wrap; gap: var(--space-1); }
@media (hover: hover) and (not (any-pointer: coarse)) {
  :global(:root[data-message-actions="hover"] .workspace .message:not(:hover, :focus-within, .actions-expanded) .message-actions) { opacity: 0; }
}
@media (hover: hover) and (pointer: fine) and (not (any-pointer: coarse)) {
  :global(:root[data-message-actions="hover"] .workspace .message:not(:hover, :focus-within) .message-copy) { opacity: 0; }
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
  padding: var(--space-2) max(var(--chat-gutter), calc((100% - var(--chat-content-width)) / 2)) max(var(--space-3), env(safe-area-inset-bottom, 0px));
  border-top: 1px solid var(--border); }
.is-start .composer-wrap { padding: 0; border: 0; }
.is-start .composer { grid-template-columns: 42px minmax(0, 1fr) auto; padding: var(--space-3); }
.is-start .composer textarea { grid-area: 1 / 1 / 2 / -1; margin-bottom: var(--space-4); }
.is-start .composer-action { grid-area: 2 / 3; display: inline-flex; width: auto; padding-inline: var(--space-3); }
.is-start .composer > .composer-attach { grid-area: 2 / 1; }
.is-start .composer-send-controls { grid-area: 2 / 3; }
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
.picked-file { display: flex; flex-wrap: wrap; align-items: baseline; gap: var(--space-1); min-width: 0; }
.picked-file > span { min-width: 0; overflow-wrap: anywhere; }
.file-list.picked .picked-error, .rejected-files { color: var(--danger); overflow-wrap: anywhere; white-space: normal; }
.picked-error { flex-basis: 100%; }
.rejected-files { grid-column: 1 / -1; font-size: var(--font-size-sm); padding-inline: var(--space-2); }
.focus-chat .file-list.picked, .focus-chat .rejected-files { max-height: min(20dvh, 9rem); overflow: auto; }
.focus-chat .file-list.picked :deep(.ui-icon-button) { min-width: 44px; min-height: 44px; }
.composer { display: grid; grid-template-columns: 42px minmax(0, 1fr) auto; gap: var(--space-1); align-items: end;
  border: 1px solid var(--composer-border); border-radius: var(--radius-lg); padding: 5px;
  background: var(--input); box-shadow: var(--elevation-1); }
.composer.text-only { grid-template-columns: minmax(0, 1fr) auto; }
.composer-actions { display: contents; }
.composer-send-controls { display: flex; align-items: center; gap: var(--space-1); }
.draft-status, .clear-draft { grid-column: 1 / -1; font-size: var(--font-size-sm); }
.draft-status { padding-inline: var(--space-2); overflow-wrap: anywhere; }
.draft-quote blockquote { white-space: pre-wrap; padding-inline-start: var(--space-2); border-inline-start: 2px solid var(--accent); }
.draft-quote :is(a, button) { min-height: 40px; display: inline-flex; align-items: center; }
.draft-conflict { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-1) var(--space-2); }
.clear-draft { justify-self: start; min-height: 28px; }
.composer .voice-trigger { min-inline-size: 44px; min-block-size: 44px; }
.composer textarea { padding: 8px 4px; line-height: 21px; border: 0; resize: none; min-height: 37px; max-height: 142px;
  overflow-y: auto; background: transparent; font-size: max(16px, 1rem); }
.composer textarea:focus-visible { outline: none; }
.composer-attach, .composer-action { display: grid; place-items: center; width: 42px; height: 42px; min-height: 42px; padding: 0; border-radius: var(--radius-md); }
.composer-attach { color: var(--text-muted); border: 0; background: transparent; }
.composer-attach:hover { color: var(--text); background: var(--surface-2); }
.stop-button, .stop-button:hover:not(:disabled) { background: var(--danger-fill); border-color: var(--danger-fill); color: white; }
.adaptation { display: flex; flex-wrap: wrap; align-items: baseline; gap: var(--space-2); }
.navigation-alert { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); }
@media (max-width: 900px) {
  .workspace { height: calc(var(--chat-viewport-height, 100dvh) - var(--header)); }
  .workspace.history-open { grid-template-columns: minmax(0, 1fr); }
  .conversation { --chat-gutter: var(--space-3); }
  .history-tools { padding-block-start: var(--space-3); }
  .chat-control, .chat-menu > summary, .history-project-add, .workspace :deep(.ui-icon-button),
  .composer :is(.composer-attach, .composer-action) { min-width: 44px; min-height: 44px; }
  .history .history-new, .history-project-list a, .thread-row a, .chat-menu-panel :is(a, button),
  .clear-draft, .draft-quote :is(a, button), .start-choices button { min-height: 44px; }
  .message.user { --message-copy-size: 44px; }
  .workspace:not(.focus-chat) .composer:not(.text-only) { grid-template-columns: 44px minmax(0, 1fr) auto; }
  .composer textarea { max-height: clamp(37px, calc(var(--chat-viewport-height, 100dvh) - var(--header) - 188px), 142px); }
  .transcript { scroll-padding-block: var(--space-2); }
  .message.assistant .message-body { font-size: var(--font-size-md); }
}
@media (max-width: 480px) {
  .conversation-header { min-height: 48px; }
  .chat-start { padding-block-start: clamp(1.5rem, 8vh, 4rem); }
  .start-break { display: none; }
}
</style>
