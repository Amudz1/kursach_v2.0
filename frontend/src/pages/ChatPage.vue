<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import ChatRulesPanel from '../components/ChatRulesPanel.vue'
import ProjectEditor from '../components/ProjectEditor.vue'
import { useChat } from '../use-chat'
import { markdown } from '../markdown'
import type { Chat, Message } from '../types'

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
const defaultIdeas = ['Объясни идею на простом примере', 'Помоги улучшить мой текст', 'Разложи задачу на шаги']
const reasons = { too_long: 'Слишком длинно', too_complex: 'Слишком сложно', inaccurate: 'Неточно', wrong_language: 'Не тот язык' }
const failureMessages: Record<string, string> = {
  ai_timeout: 'Модель не успела ответить.', ai_empty: 'Модель вернула пустой ответ.',
  ai_credentials: 'Сервис модели отклонил ключ.', ai_rate_limit: 'Сервис модели ограничил запросы.',
  generation_interrupted: 'Генерация прервалась после перезапуска.', ai_incomplete: 'Поток ответа оборвался.',
  daily_token_limit: 'Для этого сообщения не хватает токенов. Сократите запрос или проверьте бюджет на странице тарифов.',
}
const formTitle = computed(() => ({ rename: 'Переименовать диалог', delete: 'Удалить диалог?', edit: 'Изменить сообщение' })[operation.value])

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
onMounted(() => {
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
      <section class="history-projects" aria-labelledby="projects-heading">
        <div class="history-project-heading">
          <h2 id="projects-heading"><RouterLink to="/workspaces" title="Управление проектами">Проекты</RouterLink></h2>
          <button type="button" class="history-project-add" @click="projectEditor?.show()" aria-label="Новый проект" title="Новый проект">+</button>
        </div>
        <nav v-if="workspaces.length" class="history-project-list" aria-label="Список проектов">
          <RouterLink v-for="project in workspaces" :key="project.id" :to="{ path: '/chats', query: { workspace: project.id } }"
            :aria-current="workspaceId === project.id ? 'location' : undefined" @click="closeMobileHistory">
            <span class="project-folder" aria-hidden="true"></span><span>{{ project.name }}</span>
          </RouterLink>
        </nav>
      </section>
      <h2>Все диалоги</h2>
      <form class="search-form" @submit.prevent="loadChats()">
        <label class="sr-only" for="chat-search">Поиск по диалогам</label>
        <input id="chat-search" v-model="search" type="search" maxlength="120" placeholder="Поиск по диалогам">
        <button class="text-button" type="submit">Найти</button>
      </form>
      <nav aria-label="История диалогов" class="thread-list">
        <RouterLink v-for="chat in chats" :key="chat.id" :to="chatRoute(chat)" @click="closeMobileHistory">
          <span v-if="chat.isPinned" class="pin-mark" aria-label="Закреплён">◆</span>
          <span><span>{{ chat.title }}</span><small v-if="chat.workspaceId && projectNames.has(chat.workspaceId)">{{ projectNames.get(chat.workspaceId) }}</small></span>
        </RouterLink>
        <p v-if="!chats.length" class="muted">Диалогов пока нет.</p>
      </nav>
      <button v-if="chatsHaveMore" class="plain-button" @click="loadChats(false)">Ещё диалоги</button>
    </aside>

    <div class="conversation">
      <header class="conversation-header">
        <button ref="historyToggle" type="button" class="chat-control history-toggle" :aria-label="historyAction" :title="historyAction"
          :aria-expanded="historyVisible" aria-controls="chat-history" @click="toggleHistory">
          <span class="history-icon" aria-hidden="true"></span>
        </button>
        <div class="conversation-heading">
          <h1>{{ selected?.title ?? (selectedId ? 'Диалог' : currentWorkspace?.name ?? 'Новый диалог') }}</h1>
          <p v-if="selected?.expiresAt" class="help chat-scope-name" :title="new Date(selected.expiresAt).toLocaleString('ru-RU')">Временный диалог</p>
          <p v-else-if="selectedId && currentWorkspace" class="help chat-scope-name">{{ currentWorkspace.name }}</p>
          <p v-if="selectedId" class="status-line"><span :class="{ online: linked }"></span>{{ linked ? 'Подключено' : 'Подключаемся к чату' }}</p>
        </div>
        <button type="button" class="chat-control chat-rules-button" aria-haspopup="dialog" @click="rulesPanel?.show()">Правила</button>
        <button type="button" class="chat-control new-chat-control" aria-label="Новый диалог" title="Новый диалог" @click="startNewChat"><span aria-hidden="true">+</span></button>
        <details ref="chatMenu" class="chat-menu">
          <summary aria-label="Настройки диалога">•••</summary>
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

      <div ref="transcript" class="transcript" aria-label="Сообщения">
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
              <span v-if="chat.isPinned" class="pin-mark" aria-label="Закреплён">◆</span>{{ chat.title }}
            </RouterLink>
          </nav>
          <p v-else-if="!projectLoading" class="muted">В проекте пока нет диалогов.</p>
          <p v-if="projectLoading" class="muted" role="status">Загружаем диалоги проекта…</p>
          <button v-else-if="projectHasMore" type="button" class="plain-button self-start" @click="loadProjectChats(false)">Ещё диалоги проекта</button>
        </section>
        <div v-else-if="!loading && !messages.length" class="start-conversation">
          <span class="brand-mark start-mark" aria-hidden="true">b<span>.</span></span>
          <h2>С чего начнём?</h2>
          <div class="start-choices"><button v-for="idea in defaultIdeas" :key="idea" class="secondary" @click="setComposer(idea)">{{ idea }} <span aria-hidden="true">↗</span></button></div>
        </div>
        <article v-for="item in messages" :key="item.id" class="message" :class="item.role">
          <div class="message-meta">
            <span class="speaker-mark" aria-hidden="true">{{ item.role === 'user' ? '↗' : 'b.' }}</span>
            <strong>{{ item.role === 'user' ? 'Вы' : 'Budz' }}</strong>
            <time :datetime="item.createdAt">{{ time(item.createdAt) }}</time>
          </div>
          <div class="message-body">
            <div v-if="item.role === 'assistant' && item.content" class="markdown" v-html="markdown(item.content)"></div>
            <p v-else-if="item.content" class="user-text">{{ item.content }}</p>
            <p v-if="!item.content && (item.status === 'pending' || item.status === 'streaming')" class="muted" role="status">Budz готовит ответ…</p>
            <p v-if="item.status === 'failed'" class="muted" role="status">{{ failureMessages[item.errorCode ?? ''] ?? 'Ответ недоступен.' }} Можно повторить генерацию.</p>
            <p v-if="item.status === 'stopped'" class="muted" role="status">Генерация остановлена.</p>
            <ul v-if="item.attachments?.length" class="file-list">
              <li v-for="attachment in item.attachments" :key="attachment.id"><button class="text-button" @click="download(attachment)">Скачать {{ attachment.fileName }}</button></li>
            </ul>
          </div>
          <div v-if="item.status !== 'pending' && item.status !== 'streaming'" class="message-actions">
            <button v-if="item.rules" class="text-button" aria-haspopup="dialog" @click="rulesPanel?.show(item.rules)">Правила ответа</button>
            <button v-if="item.content" class="text-button" @click="copy(item)">{{ copied === item.id ? 'Скопировано' : 'Копировать' }}</button>
            <button v-if="item.id === lastUser?.id && !flow" class="text-button" @click="showDialog('edit')">Изменить</button>
            <button v-if="item.id === lastAnswer?.id && !flow" class="text-button" :disabled="busy" @click="revise(item)">Повторить</button>
            <template v-if="!temporary && item.role === 'assistant' && item.status === 'completed'">
              <button class="text-button" :aria-pressed="item.rating === 1" @click="rate(item, 1)">Полезно</button>
              <button class="text-button" :aria-pressed="item.rating === -1" @click="feedbackTarget = feedbackTarget === item.id ? '' : item.id">Не помогло</button>
            </template>
          </div>
          <div v-if="feedbackTarget === item.id" class="feedback-reasons" aria-label="Причина оценки">
            <button v-for="(label, code) in reasons" :key="code" class="secondary" @click="rate(item, -1, code); feedbackTarget = ''">{{ label }}</button>
            <button class="text-button" @click="rate(item, -1); feedbackTarget = ''">Без причины</button>
          </div>
          <div v-if="item.id === lastAnswer?.id && item.suggestions.length" class="suggestions" aria-label="Вопросы для продолжения">
            <button v-for="idea in item.suggestions" :key="idea" class="secondary" @click="setComposer(idea)">{{ idea }}</button>
          </div>
        </article>
      </div>

      <div class="composer-wrap">
        <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
        <div v-if="adaptation && !temporary" class="notice adaptation" role="status">
          <span>Последние оценки предлагают сделать ответы короче.</span>
          <button class="text-button" @click="decide(true)">Применить</button>
          <button class="text-button" @click="decide(false)">Скрыть</button>
        </div>
        <form class="composer" :class="{ 'text-only': temporary }" @submit.prevent="send">
          <input ref="picker" class="sr-only" type="file" tabindex="-1" aria-label="Выбор файла" multiple accept=".txt,.md,.pdf,.docx,.png,.jpg,.jpeg" @change="addedFiles">
          <button v-if="!temporary" type="button" class="composer-attach" aria-label="Прикрепить" title="Прикрепить" @click="picker?.click()"><span aria-hidden="true">+</span></button>
          <label class="sr-only" for="composer">Сообщение для Budz</label>
          <textarea id="composer" ref="composer" :value="draft" placeholder="Сообщение для Budz…" rows="1" maxlength="10000"
            @input="changeDraft(($event.target as HTMLTextAreaElement).value)" @keydown.ctrl.enter.prevent="send"></textarea>
          <button v-if="flow" type="button" class="primary composer-action stop-button" aria-label="Остановить" title="Остановить" @click="stop"><span aria-hidden="true">■</span></button>
          <button v-else type="submit" class="primary composer-action" aria-label="Отправить" title="Отправить" :disabled="busy || (!draft.trim() && !files.length)"><span aria-hidden="true">↑</span></button>
          <ul v-if="files.length" class="file-list picked">
            <li v-for="(file, index) in files" :key="`${file.name}-${index}`">
              <span>{{ file.name }}</span><button class="text-button" type="button" :aria-label="`Удалить ${file.name}`" @click="removeFile(index)">×</button>
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
