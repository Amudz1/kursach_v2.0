<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useChat } from '../use-chat'
import { markdown } from '../markdown'
import { expertiseOptions, labelFor, languageOptions, lengthOptions, toneOptions } from '../profile-options'
import type { Message } from '../types'

const {
  session, chats, messages, selected, selectedId, personaId, personas, profile, model, currentPlan, search, nextBefore,
  chatsHaveMore, draft, files, busy, loading, error, linked, flow, lastUser, lastAnswer, adaptation, transcript,
  loadChats, loadMessages, newChat, changePersona, patch, remove, changeDraft, chooseFiles, removeFile, send, stop, revise, rate,
  decide, download,
} = useChat()
const activePersona = computed(() => personas.value.find((persona) => persona.id === personaId.value))
const usage = computed(() => session.account?.usage)
const drawer = ref<HTMLDetailsElement>()
const picker = ref<HTMLInputElement>()
const dialog = ref<HTMLDialogElement>()
const operation = ref<'rename' | 'delete' | 'edit'>('rename')
const modalText = ref('')
const copied = ref('')
const feedbackTarget = ref('')
const composer = ref<HTMLTextAreaElement>()
const defaultIdeas = ['Разбери эту тему по шагам', 'Проверь мой текст', 'Помоги составить план работы']
const reasons = { too_long: 'Слишком длинно', too_complex: 'Слишком сложно', inaccurate: 'Неточно', wrong_language: 'Не тот язык' }
const failureMessages: Record<string, string> = {
  ai_timeout: 'Модель не успела ответить.', ai_empty: 'Модель вернула пустой ответ.',
  ai_credentials: 'Сервис модели отклонил ключ.', ai_rate_limit: 'Сервис модели ограничил запросы.',
  generation_interrupted: 'Генерация прервалась после перезапуска.', ai_incomplete: 'Поток ответа оборвался.',
}
const formTitle = computed(() => ({ rename: 'Переименовать диалог', delete: 'Удалить диалог?', edit: 'Изменить сообщение' })[operation.value])

function setComposer(text: string) {
  changeDraft(text)
  composer.value?.focus()
}
function showDialog(type: typeof operation.value) {
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

function time(value: string) {
  return new Intl.DateTimeFormat('ru', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function closeMobileHistory() {
  if (window.matchMedia('(max-width: 780px)').matches && drawer.value) drawer.value.open = false
}
onMounted(closeMobileHistory)
</script>

<template>
  <section class="workspace" aria-label="Диалоги Budz">
    <details ref="drawer" class="history" open>
      <summary>Диалоги</summary>
      <div class="history-body">
        <div class="history-heading"><h2>Диалоги</h2><button class="secondary square" @click="newChat" aria-label="Новый диалог">+</button></div>
        <form class="search-form" @submit.prevent="loadChats()">
          <label class="sr-only" for="chat-search">Поиск по диалогам</label>
          <input id="chat-search" v-model="search" type="search" maxlength="120" placeholder="Поиск по диалогам">
          <button class="text-button" type="submit">Найти</button>
        </form>
        <nav aria-label="История диалогов" class="thread-list">
          <RouterLink v-for="chat in chats" :key="chat.id" :to="`/chats/${chat.id}`" @click="closeMobileHistory">
            <span v-if="chat.isPinned" class="pin-mark" aria-label="Закреплён">◆</span>
            <span>{{ chat.title }}</span>
          </RouterLink>
          <p v-if="!chats.length" class="muted">Диалогов пока нет.</p>
        </nav>
        <button v-if="chatsHaveMore" class="plain-button" @click="loadChats(false)">Ещё диалоги</button>
      </div>
    </details>

    <div class="conversation">
      <header class="conversation-header">
        <div class="conversation-heading">
          <h1>{{ selected?.title ?? (selectedId ? 'Диалог' : 'Новый диалог') }}</h1>
          <p v-if="selectedId" class="status-line"><span :class="{ online: linked }"></span>{{ linked ? 'Подключено' : 'Подключаемся к чату' }}</p>
        </div>
        <div v-if="selectedId" class="chat-actions">
          <button class="text-button" @click="patch({ isPinned: !selected?.isPinned })">{{ selected?.isPinned ? 'Открепить' : 'Закрепить' }}</button>
          <button class="text-button" @click="showDialog('rename')">Переименовать</button>
          <button class="text-button danger-text" @click="showDialog('delete')">Удалить</button>
        </div>
      </header>

      <div ref="transcript" class="transcript" aria-label="Сообщения">
        <p v-if="loading && !messages.length" class="muted" role="status">Загружаем сообщения…</p>
        <button v-if="nextBefore" class="plain-button" @click="loadMessages(false, true)">Загрузить ранние сообщения</button>
        <div v-if="!loading && !messages.length" class="start-conversation">
          <div aria-hidden="true" class="start-symbol">↳</div>
          <h2>С чего начнём?</h2>
          <p>{{ activePersona?.description || 'Отправьте вопрос или приложите файл.' }}</p>
          <div class="start-choices"><button v-for="idea in defaultIdeas" :key="idea" class="secondary" @click="setComposer(idea)">{{ idea }}</button></div>
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
            <button v-if="item.content" class="text-button" @click="copy(item)">{{ copied === item.id ? 'Скопировано' : 'Копировать' }}</button>
            <button v-if="item.id === lastUser?.id && !flow" class="text-button" @click="showDialog('edit')">Изменить</button>
            <button v-if="item.id === lastAnswer?.id && !flow" class="text-button" :disabled="busy" @click="revise(item)">Повторить</button>
            <template v-if="item.role === 'assistant' && item.status === 'completed'">
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
        <div v-if="adaptation" class="notice adaptation" role="status">
          <span>Последние оценки предлагают сделать ответы короче.</span>
          <button class="text-button" @click="decide(true)">Применить</button>
          <button class="text-button" @click="decide(false)">Скрыть</button>
        </div>
        <form class="composer" @submit.prevent="send">
          <label class="sr-only" for="composer">Сообщение для Budz</label>
          <textarea id="composer" ref="composer" :value="draft" placeholder="Сообщение для Budz…" rows="2" maxlength="10000"
            @input="changeDraft(($event.target as HTMLTextAreaElement).value)" @keydown.ctrl.enter.prevent="send"></textarea>
          <ul v-if="files.length" class="file-list picked">
            <li v-for="(file, index) in files" :key="`${file.name}-${index}`">
              <span>{{ file.name }}</span><button class="text-button" type="button" :aria-label="`Удалить ${file.name}`" @click="removeFile(index)">×</button>
            </li>
          </ul>
          <div class="composer-tools">
            <div class="compose-options">
              <label><span>Персона</span><select :value="personaId" aria-label="Текущая персона" @change="changePersona(($event.target as HTMLSelectElement).value)">
                <option v-if="!personaId" value="">По умолчанию</option><option v-for="persona in personas" :key="persona.id" :value="persona.id">{{ persona.name }}</option>
              </select></label>
              <label><span>Модель</span><select :value="model" aria-label="Модель" @change="setModel">
                <option value="">Автоматически</option><option v-for="allowed in currentPlan?.allowedModels" :key="allowed" :value="allowed">{{ allowed }}</option>
              </select></label>
            </div>
            <div class="compose-actions">
              <input ref="picker" class="sr-only" type="file" tabindex="-1" aria-label="Выбор файла" multiple accept=".txt,.md,.pdf,.docx,.png,.jpg,.jpeg" @change="addedFiles">
              <button type="button" class="secondary" @click="picker?.click()">Прикрепить</button>
              <button v-if="flow" type="button" class="primary stop-button" @click="stop">Остановить</button>
              <button v-else type="submit" class="primary" :disabled="busy || (!draft.trim() && !files.length)">Отправить <span aria-hidden="true">↗</span></button>
            </div>
          </div>
        </form>
      </div>
    </div>

    <aside class="chat-context" aria-label="Параметры диалога">
      <div class="context-heading"><span class="preview-symbol" aria-hidden="true">↳</span><h2>Параметры диалога</h2></div>
      <div class="context-persona"><strong>{{ activePersona?.name ?? 'Универсальный помощник' }}</strong><p v-if="activePersona?.description">{{ activePersona.description }}</p></div>
      <dl v-if="profile" class="context-list">
        <div><dt>Язык</dt><dd>{{ labelFor(languageOptions, profile.language) }}</dd></div>
        <div><dt>Тон</dt><dd>{{ labelFor(toneOptions, profile.tone) }}</dd></div>
        <div><dt>Уровень</dt><dd>{{ labelFor(expertiseOptions, profile.expertiseLevel) }}</dd></div>
        <div><dt>Длина</dt><dd>{{ labelFor(lengthOptions, profile.responseLength) }}</dd></div>
      </dl>
      <RouterLink class="text-button" to="/personalization">Изменить настройки ↗</RouterLink>
      <div v-if="usage" class="context-usage"><strong>{{ usage.remainingPrompts ?? '∞' }}</strong>
        <span>{{ usage.remainingPrompts === null ? 'Без дневного лимита' : 'Запросов осталось сегодня' }}</span>
        <RouterLink class="text-button" to="/plans">{{ usage.planName }} ↗</RouterLink>
      </div>
    </aside>

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
  </section>
</template>
