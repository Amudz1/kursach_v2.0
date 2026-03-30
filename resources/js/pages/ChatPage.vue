<template>
  <div class="chat-layout">
    <!-- Левая панель: список чатов -->
    <aside class="chat-sidebar">
      <div class="sidebar-header">
        <h2>Чаты</h2>
        <button class="btn-new-chat" @click="createNewChat" title="Новый чат">＋</button>
      </div>

      <!-- Лимит промтов -->
      <div v-if="!auth.hasSubscription" class="prompt-limit-box">
        <div class="limit-bar-wrap">
          <div class="limit-bar" :style="{ width: limitPercent + '%' }" :class="limitClass"></div>
        </div>
        <p>{{ auth.remainingPrompts }} / {{ FREE_LIMIT }} запросов</p>
        <router-link to="/subscription" class="limit-upgrade">✨ Снять лимит</router-link>
      </div>
      <div v-else class="pro-badge-sidebar">⭐ PRO — безлимитный доступ</div>

      <!-- Список чатов -->
      <div class="chat-list" ref="chatListEl">
        <div v-if="chatStore.chats.length === 0" class="empty-chats">
          <p>Нет чатов</p>
          <p>Нажмите ＋ чтобы начать</p>
        </div>
        <div
          v-for="chat in chatStore.chats"
          :key="chat.id"
          class="chat-item"
          :class="{ active: chatStore.activeChatId === chat.id }"
          @click="openChat(chat.id)"
        >
          <div class="chat-item-icon">💬</div>
          <div class="chat-item-info">
            <div class="chat-item-title">{{ chat.title || 'Новый чат' }}</div>
            <div class="chat-item-meta">{{ chat.message_count || 0 }} сообщений</div>
          </div>
          <button class="chat-item-delete" @click.stop="deleteChat(chat.id)" title="Удалить">🗑</button>
        </div>
      </div>
    </aside>

    <!-- Основная область чата -->
    <div class="chat-main">

      <!-- Пустое состояние (нет активного чата) -->
      <div v-if="!chatStore.activeChatId" class="chat-welcome">
        <div class="welcome-icon">🤖</div>
        <h2>Добро пожаловать в AI Helper Budz</h2>
        <p>Выберите чат из списка слева или начните новый диалог</p>
        <button class="btn btn-primary btn--lg" @click="createNewChat">
          💬 Начать новый чат
        </button>
        <div class="welcome-tips">
          <div v-for="tip in tips" :key="tip" class="tip-chip" @click="startWithTip(tip)">{{ tip }}</div>
        </div>
      </div>

      <!-- Активный чат -->
      <template v-else>
        <!-- Шапка чата -->
        <div class="chat-header">
          <div class="chat-header-info">
            <span class="chat-header-icon">💬</span>
            <span class="chat-header-title">{{ activeChat?.title || 'Новый чат' }}</span>
          </div>
          <div class="chat-header-model">
            <span class="model-chip">🧠 DeepSeek AI</span>
          </div>
        </div>

        <!-- Сообщения -->
        <div class="messages-area" ref="messagesEl">
          <div v-if="chatStore.loading" class="messages-loading">
            <span class="spinner"></span> Загрузка...
          </div>

          <div v-else>
            <div
              v-for="msg in chatStore.messages"
              :key="msg.id"
              :class="['message-wrap', msg.role === 'user' ? 'msg-right' : 'msg-left']"
            >
              <!-- Аватар AI -->
              <div v-if="msg.role === 'assistant'" class="msg-avatar">🤖</div>

              <div :class="['message-bubble', msg.role === 'user' ? 'bubble-user' : (msg.is_error ? 'bubble-error' : 'bubble-ai')]">
                <!-- Вложение -->
                <div v-if="msg.has_attachment" class="msg-attachment">
                  📎 {{ msg.attachment_name }}
                </div>
                <!-- Текст (рендер markdown) -->
                <div class="msg-content" v-html="renderMarkdown(msg.content)"></div>
                <div class="msg-time">{{ msg.created_at }}</div>
              </div>

              <!-- Аватар пользователя
              <div v-if="msg.role === 'user'" class="msg-avatar msg-avatar--user">
                {{ auth.username[0]?.toUpperCase() }}
              </div> -->
            </div>

            <!-- Typing индикатор -->
            <div v-if="chatStore.sending" class="message-wrap msg-left">
              <div class="msg-avatar">🤖</div>
              <div class="message-bubble bubble-ai">
                <div class="typing-indicator">
                  <span></span><span></span><span></span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Предупреждение об лимите -->
        <div v-if="limitReached" class="limit-warning">
          ⚠ Лимит запросов исчерпан.
          <router-link to="/subscription">Оформить подписку →</router-link>
        </div>

        <!-- Поле ввода -->
        <div class="input-area">
          <!-- Превью файла -->
          <div v-if="selectedFile" class="file-preview">
            <span>📎 {{ selectedFile.name }}</span>
            <button @click="selectedFile = null">✕</button>
          </div>

          <div class="input-row">
            <!-- Кнопка прикрепить файл -->
            <label class="attach-btn" title="Прикрепить файл">
              📎
              <input type="file" @change="onFileSelect" accept=".txt,.pdf,.doc,.docx,.png,.jpg,.jpeg,.gif" hidden/>
            </label>

            <!-- Textarea -->
            <textarea
              v-model="inputText"
              class="chat-textarea"
              :placeholder="chatStore.sending ? 'AI отвечает...' : 'Напишите сообщение... (Enter — отправить)'"
              :disabled="chatStore.sending || limitReached"
              @keydown.enter.exact.prevent="sendMessage"
              @keydown.enter.shift.exact="() => {}"
              rows="1"
              ref="textareaEl"
              @input="autoResize"
            ></textarea>

            <!-- Отправить -->
            <button
              class="send-btn"
              :disabled="(!inputText.trim() && !selectedFile) || chatStore.sending || limitReached"
              @click="sendMessage"
            >
              <span v-if="chatStore.sending" class="spinner spinner--white"></span>
              <span v-else>➤</span>
            </button>
          </div>
          <p class="input-hint">Enter — отправить · Shift+Enter — перенос строки</p>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useChatStore } from '../stores/chat'
import { marked } from 'marked'
import DOMPurify from 'dompurify'

const FREE_LIMIT = 12
const route      = useRoute()
const router     = useRouter()
const auth       = useAuthStore()
const chatStore  = useChatStore()

const inputText    = ref('')
const selectedFile = ref(null)
const limitReached = ref(false)
const messagesEl   = ref(null)
const textareaEl   = ref(null)

const activeChat   = computed(() => chatStore.chats.find(c => c.id === chatStore.activeChatId))
const limitPercent = computed(() => {
  const used = FREE_LIMIT - (auth.remainingPrompts || 0)
  return Math.min(100, (used / FREE_LIMIT) * 100)
})
const limitClass   = computed(() => {
  if (limitPercent.value > 80) return 'bar-red'
  if (limitPercent.value > 50) return 'bar-orange'
  return 'bar-green'
})

const tips = [
  'Напиши код на Python',
  'Объясни нейронные сети',
  'Помоги с резюме',
  'Переведи текст',
  'Идеи для проекта',
]

onMounted(async () => {
  await chatStore.loadChats()
  const id = route.params.id ? Number(route.params.id) : null
  if (id) openChat(id)
})

async function openChat(id) {
  router.replace(`/chat/${id}`)
  await chatStore.loadMessages(id)
  scrollToBottom()
}

async function createNewChat() {
  const chat = await chatStore.createChat()
  openChat(chat.id)
}

async function deleteChat(id) {
  if (!confirm('Удалить чат?')) return
  const wasActive = chatStore.activeChatId === id
  await chatStore.deleteChat(id)
  if (wasActive) router.replace('/chat')
}

async function sendMessage() {
    const text = inputText.value.trim();
    if ((!text && !selectedFile.value) || chatStore.sending) return;

    limitReached.value = false;
    const file = selectedFile.value;
    const savedText = text;

    // 1. OPTIMISTIC: только НАШЕ сообщение
    const tempId = `temp-${Date.now()}`;
    chatStore.messages.push({
        id: tempId,
        role: 'user',
        content: savedText || `Прикреплён файл: ${file?.name}`,
        attachment_name: file?.name || null,
        has_attachment: !!file,
        created_at: new Date().toLocaleTimeString([], {hour: '2-digit', minute: '2-digit'})
    });

    // Очищаем input
    inputText.value = '';
    selectedFile.value = null;
    resetTextarea();

    try {
        chatStore.sending = true;
        
        // 2. Store сам все сделает!
        const result = await chatStore.sendMessage(chatStore.activeChatId, savedText, file);

        // 3. УДАЛЯЕМ optimistic (store добавит реальное)
        chatStore.messages = chatStore.messages.filter(m => m.id !== tempId);

        // Лимит
        if (result.remaining_prompts !== undefined) {
            auth.updateUser({ remaining_prompts: result.remaining_prompts });
        }

    } catch (e) {
        // Rollback
        // НЕ удаляем — store сам обработает
        if (e.response?.status === 403) {
            limitReached.value = true;
        } else {
            inputText.value = savedText;
            chatStore.pushErrorMessage(e.response?.data?.message || 'Ошибка.');
        }
    } finally {
        chatStore.sending = false;
    }

    await nextTick();
    scrollToBottom();
}

function onFileSelect(e) {
  selectedFile.value = e.target.files[0] || null
}

function startWithTip(tip) {
  createNewChat().then(() => {
    inputText.value = tip
    nextTick(() => textareaEl.value?.focus())
  })
}

function renderMarkdown(text) {
  return DOMPurify.sanitize(marked.parse(text || ''))
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesEl.value) {
      messagesEl.value.scrollTop = messagesEl.value.scrollHeight
    }
  })
}

function autoResize() {
  if (!textareaEl.value) return
  textareaEl.value.style.height = 'auto'
  textareaEl.value.style.height = Math.min(textareaEl.value.scrollHeight, 160) + 'px'
}

function resetTextarea() {
  if (textareaEl.value) textareaEl.value.style.height = 'auto'
}

watch(() => chatStore.messages.length, () => scrollToBottom())
</script>

<style scoped>
.chat-layout {
  display: flex;
  height: calc(100vh - 64px);
  overflow: hidden;
}

/* ── Sidebar ───────────────────────────────────────── */
.chat-sidebar {
  width: 160px;
  min-width: 140px;
  background: var(--bg-card);
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  border-bottom: 1px solid var(--border);
}
.sidebar-header h2 { font-size: 1rem; font-weight: 700; }
.btn-new-chat {
  width: 32px; height: 32px;
  background: var(--accent);
  border: none; border-radius: 8px;
  color: #fff; font-size: 1.2rem;
  cursor: pointer; display: flex; align-items: center; justify-content: center;
  transition: background .2s;
}
.btn-new-chat:hover { background: var(--accent-dark); }

.prompt-limit-box {
  padding: 12px 16px;
  background: rgba(251,146,60,.05);
  border-bottom: 1px solid var(--border);
}
.limit-bar-wrap { height: 5px; background: var(--bg); border-radius: 3px; margin-bottom: 6px; }
.limit-bar { height: 100%; border-radius: 3px; transition: width .3s; }
.bar-green  { background: var(--green); }
.bar-orange { background: var(--orange); }
.bar-red    { background: var(--red); }
.prompt-limit-box p { font-size: 12px; color: var(--text-muted); }
.limit-upgrade { font-size: 12px; color: var(--accent); }

.pro-badge-sidebar {
  padding: 10px 16px;
  font-size: 12px;
  color: var(--green);
  background: rgba(74,222,128,.05);
  border-bottom: 1px solid var(--border);
}

.chat-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}
.empty-chats { padding: 24px 16px; text-align: center; color: var(--text-muted); font-size: 13px; }

.chat-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: var(--radius-sm);
  cursor: pointer;
  transition: background .15s;
  position: relative;
}
.chat-item:hover  { background: var(--bg-hover); }
.chat-item.active { background: var(--accent-glow); }
.chat-item-icon   { font-size: 1.1rem; flex-shrink: 0; }
.chat-item-info   { flex: 1; min-width: 0; }
.chat-item-title  { font-size: 13px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.chat-item-meta   { font-size: 11px; color: var(--text-muted); margin-top: 2px; }
.chat-item-delete {
  display: none;
  background: none; border: none;
  color: var(--text-muted); font-size: 14px;
  cursor: pointer; padding: 2px 4px; border-radius: 4px;
}
.chat-item:hover .chat-item-delete { display: block; }
.chat-item-delete:hover { color: var(--red); }

/* ── Main ──────────────────────────────────────────── */
.chat-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--bg);
}

/* Welcome */
.chat-welcome {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  padding: 40px;
  text-align: center;
}
.welcome-icon { font-size: 4rem; animation: float 3s ease-in-out infinite; }
@keyframes float { 0%,100%{transform:translateY(0)}50%{transform:translateY(-10px)} }
.chat-welcome h2     { font-size: 1.6rem; font-weight: 700; }
.chat-welcome p      { color: var(--text-muted); }
.welcome-tips        { display: flex; flex-wrap: wrap; gap: 8px; justify-content: center; margin-top: 8px; }
.tip-chip {
  padding: 7px 14px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 20px;
  font-size: 13px;
  cursor: pointer;
  transition: all .2s;
}
.tip-chip:hover { border-color: var(--accent); color: var(--accent); }

/* Chat header */
.chat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 20px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border);
}
.chat-header-info  { display: flex; align-items: center; gap: 10px; }
.chat-header-icon  { font-size: 1.2rem; }
.chat-header-title { font-weight: 700; }
.model-chip {
  font-size: 12px;
  padding: 4px 10px;
  background: var(--accent-glow);
  color: var(--accent);
  border-radius: 20px;
}

/* Messages */
.messages-area {
  flex: 1;
  overflow-y: auto;
  padding: 20px 20px 20px 160px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}
.messages-loading { display: flex; align-items: center; gap: 10px; color: var(--text-muted); padding: 20px; }

.message-wrap {
  display: flex;
  align-items: flex-end;
  gap: 8px;
  max-width: 80%;
  margin-bottom: 10px;
}
.msg-left  { align-self: flex-start; }
.msg-right { align-self: flex-end; flex-direction: row-reverse; }

.msg-avatar {
  width: 34px; height: 34px;
  border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: 1rem;
  background: var(--bg-card);
  border: 1px solid var(--border);
  flex-shrink: 0;
  font-weight: 700;
}
.msg-avatar--user {
  background: var(--accent);
  color: #fff;
  border-color: var(--accent);
}

.message-bubble {
  padding: 12px 16px;
  border-radius: 16px;
  max-width: 100%;
  word-break: break-word;
}
.bubble-user {
  background: var(--accent);
  color: #fff;
  border-bottom-right-radius: 4px;
}
.bubble-error {
  background: rgba(248,113,113,.1);
  border: 1px solid rgba(248,113,113,.3);
  color: var(--red);
  border-bottom-left-radius: 4px;
}

.bubble-ai {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-bottom-left-radius: 4px;
}

.msg-attachment {
  font-size: 12px;
  opacity: .8;
  margin-bottom: 6px;
  padding: 4px 8px;
  background: rgba(255,255,255,.1);
  border-radius: 6px;
}
.msg-content { font-size: 14px; line-height: 1.65; }
.msg-content :deep(pre)  { background: rgba(0,0,0,.3); padding: 12px; border-radius: 8px; overflow-x: auto; margin: 8px 0; }
.msg-content :deep(code) { font-family: monospace; font-size: 13px; }
.msg-content :deep(p)    { margin-bottom: 8px; }
.msg-content :deep(p:last-child) { margin-bottom: 0; }
.msg-content :deep(ul), .msg-content :deep(ol) { padding-left: 20px; margin: 8px 0; }
.msg-time { font-size: 11px; opacity: .5; margin-top: 6px; text-align: right; }

/* Typing */
.typing-indicator { display: flex; gap: 4px; padding: 4px 2px; }
.typing-indicator span {
  width: 8px; height: 8px;
  background: var(--text-muted);
  border-radius: 50%;
  animation: typing 1.2s infinite;
}
.typing-indicator span:nth-child(2) { animation-delay: .2s; }
.typing-indicator span:nth-child(3) { animation-delay: .4s; }
@keyframes typing { 0%,60%,100%{transform:translateY(0)}30%{transform:translateY(-6px)} }

/* Limit warning */
.limit-warning {
  padding: 10px 20px;
  background: rgba(248,113,113,.1);
  border-top: 1px solid rgba(248,113,113,.3);
  font-size: 13px;
  color: var(--red);
  text-align: center;
}

/* Input area */
.input-area {
  padding: 12px 16px 16px;
  background: var(--bg-card);
  border-top: 1px solid var(--border);
}

.file-preview {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 12px;
  background: var(--bg-input);
  border-radius: var(--radius-sm);
  font-size: 13px;
  margin-bottom: 8px;
}
.file-preview button { background: none; border: none; color: var(--text-muted); cursor: pointer; font-size: 16px; }

.input-row { display: flex; align-items: flex-end; gap: 8px; }

.attach-btn {
  width: 40px; height: 40px; flex-shrink: 0;
  display: flex; align-items: center; justify-content: center;
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  cursor: pointer; font-size: 1.1rem;
  transition: border-color .2s;
}
.attach-btn:hover { border-color: var(--accent); }

.chat-textarea {
  flex: 1;
  min-height: 40px;
  max-height: 160px;
  padding: 10px 14px;
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  color: var(--text);
  font-size: 15px;
  font-family: inherit;
  resize: none;
  outline: none;
  line-height: 1.5;
  transition: border-color .2s;
  overflow-y: auto;
}
.chat-textarea:focus { border-color: var(--accent); }
.chat-textarea:disabled { opacity: .5; }

.send-btn {
  width: 40px; height: 40px; flex-shrink: 0;
  background: var(--accent);
  border: none; border-radius: var(--radius-sm);
  color: #fff; font-size: 1.1rem;
  cursor: pointer; display: flex; align-items: center; justify-content: center;
  transition: all .2s;
}
.send-btn:hover:not(:disabled) { background: var(--accent-dark); }
.send-btn:disabled { opacity: .4; cursor: not-allowed; }

.input-hint { font-size: 11px; color: var(--text-muted); margin-top: 6px; text-align: center; }

.spinner--white { border-color: rgba(255,255,255,.3); border-top-color: #fff; }
</style>
