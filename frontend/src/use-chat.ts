import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { ApiError, api, apiResponse, explain } from './api'
import { useSession } from './session'
import { appearance } from './appearance'
import { validateAttachmentSelection } from './chat-attachments'
import { messageLimit, useChatDrafts, type DraftQuote, type DraftSession } from './chat-drafts'
import { quotedRequest, submittedLength } from './chat-quotes'
import { readReadingAnchor, useChatResume, type ReadingAnchor } from './use-chat-resume'
import type { Adaptation, Attachment, Chat, Message, MessagePage, Page, Persona, Plan, Workspace } from './types'

const terminal = (message: Message) => message.status !== 'pending' && message.status !== 'streaming'
type TranscriptPosition = { chatId: string; atEnd: boolean; anchor?: ReadingAnchor; scrollTop: number; focusedId?: string }

export function useChat() {
  const route = useRoute()
  const router = useRouter()
  const session = useSession()
  const selectedId = computed(() => typeof route.params.id === 'string' ? route.params.id : '')
  const chats = ref<Chat[]>([])
  const workspaces = ref<Workspace[]>([])
  const projectChats = ref<Chat[]>([])
  const projectHasMore = ref(false)
  const projectLoading = ref(false)
  const projectPage = ref(1)
  const detail = ref<Chat>()
  const messages = ref<Message[]>([])
  const selected = computed(() => chats.value.find((chat) => chat.id === selectedId.value) ??
    (detail.value?.id === selectedId.value ? detail.value : undefined))
  const workspaceId = computed(() => selected.value ? selected.value.workspaceId ?? '' :
    typeof route.query.workspace === 'string' ? route.query.workspace : '')
  const currentWorkspace = computed(() => workspaces.value.find((workspace) => workspace.id === workspaceId.value))
  const workspaceQuery = computed(() => workspaceId.value ? { workspace: workspaceId.value } : {})
  const plans = ref<Plan[]>([])
  const currentPlan = computed(() => plans.value.find((plan) => plan.code === session.account?.usage.planCode))
  const model = ref('')
  const search = ref('')
  const nextBefore = ref<string | null>(null)
  const nextAfter = ref<string | null>(null)
  const loadingPage = ref(false)
  const highlightedId = ref('')
  const messageNavigationError = ref('')
  const offscreenFlow = ref<Message>()
  const bookmarkPending = ref(new Set<string>())
  const chatsHaveMore = ref(false)
  const page = ref(1)
  const owner = computed(() => session.account?.id ?? '')
  const drafts = useChatDrafts(owner, selectedId, workspaceId, selected)
  const draft = computed(() => drafts.current.value.text)
  const draftQuote = computed(() => drafts.current.value.quote)
  const files = computed(() => drafts.current.value.files)
  const rejectedFiles = computed(() => drafts.current.value.rejectedFiles)
  const fileErrors = computed(() => drafts.current.value.fileErrors)
  const draftRestored = computed(() => drafts.current.value.restored)
  const draftHadFiles = computed(() => drafts.current.value.hadFiles && !files.value.length)
  const draftConflict = computed(() => !!drafts.current.value.external)
  const busy = ref(false)
  const loading = ref(false)
  const error = ref('')
  const linked = ref(false)
  const adaptation = ref<Adaptation>()
  const hub = shallowRef<HubConnection>()
  const flow = computed(() => offscreenFlow.value ?? messages.value.findLast((message) => !terminal(message)))
  const lastAnswer = computed(() => nextAfter.value ? undefined : messages.value.findLast((message) => message.role === 'assistant'))
  const transcript = shallowRef<HTMLElement>()
  const personas = ref<Persona[]>([])
  const reading = useChatResume(owner, selectedId, workspaceId, selected, transcript)
  let detachedPosition: TranscriptPosition | undefined
  let requestSignal: AbortController | undefined
  let hubStarting: Promise<void> | undefined
  let timer: ReturnType<typeof setInterval> | undefined
  let navigationAttempt = 0
  let disposed = false
  let gap = false
  let projectRequest = 0
  let highlightTimer: ReturnType<typeof setTimeout> | undefined

  async function loadChats(reset = true) {
    const wanted = reset ? 1 : page.value + 1
    const query = new URLSearchParams({ search: search.value, page: String(wanted) })
    try {
      const result = await api<Page<Chat>>(`/chats?${query}`)
      if (search.value !== query.get('search')) return
      chats.value = reset ? result.items : [...chats.value, ...result.items]
      chatsHaveMore.value = result.hasMore
      page.value = wanted
    } catch (reason) { error.value = explain(reason) }
  }

  async function loadWorkspaces() {
    try { workspaces.value = await api<Workspace[]>('/workspaces') }
    catch (reason) { error.value = explain(reason) }
  }

  async function loadProjectChats(reset = true) {
    const workspace = workspaceId.value
    const version = ++projectRequest
    if (reset) { projectChats.value = []; projectHasMore.value = false; projectPage.value = 1 }
    projectLoading.value = false
    if (!workspace || selectedId.value) return
    projectLoading.value = true
    const wanted = reset ? 1 : projectPage.value + 1
    const params = new URLSearchParams({ workspaceId: workspace, page: String(wanted) })
    try {
      const result = await api<Page<Chat>>(`/chats?${params}`)
      if (version !== projectRequest) return
      projectChats.value = reset ? result.items : [...projectChats.value, ...result.items]
      projectHasMore.value = result.hasMore
      projectPage.value = wanted
    } catch (reason) { if (version === projectRequest) error.value = explain(reason) }
    finally { if (version === projectRequest) projectLoading.value = false }
  }

  async function loadSelected(id: string) {
    try {
      const chat = await api<Chat>(`/chats/${id}`)
      if (id === selectedId.value) detail.value = chat
    } catch (reason) { if (id === selectedId.value) error.value = explain(reason) }
  }

  function openProject(id: string) {
    return router.push({ path: '/chats', query: { workspace: id } })
  }

  async function projectRemoved(id: string) {
    if (id === workspaceId.value) {
      const chat = selectedId.value
      await router.replace({ path: chat ? `/chats/${chat}` : '/chats' })
      if (chat) await loadSelected(chat)
    }
    await Promise.all([loadWorkspaces(), loadChats()])
  }

  function updateMessage(item: Message) {
    const old = messages.value.find((message) => message.id === item.id)
    if (old) Object.assign(old, item)
    else messages.value.push(item)
  }

  function newPlaceholder(id: string): Message {
    return { id, role: 'assistant', content: '', status: 'streaming', model: model.value || null, errorCode: null,
      suggestions: [], createdAt: new Date().toISOString(), triggerMessageId: null, rating: null, feedbackReason: null }
  }

  function readingPosition(): TranscriptPosition | undefined {
    const area = transcript.value
    const chatId = selectedId.value
    if (!area || !chatId || !area.clientHeight) return detachedPosition?.chatId === chatId ? detachedPosition : undefined
    const focused = area.ownerDocument?.activeElement as HTMLElement | undefined
    const focusedId = focused && area.contains(focused) ? focused.dataset.readingMessage : undefined
    return { chatId, anchor: readReadingAnchor(area), scrollTop: area.scrollTop, focusedId,
      atEnd: !nextAfter.value && area.scrollHeight - area.scrollTop - area.clientHeight <= 24 }
  }

  function scrollToReadingAnchor(area: HTMLElement, target: HTMLElement, offset: number, behavior: ScrollBehavior) {
    const top = target.getBoundingClientRect().top - area.getBoundingClientRect().top
    const clipped = Math.min(offset, Math.max(0, target.offsetHeight - 8))
    area.scrollTo({ top: area.scrollTop + top + clipped - 8, behavior })
  }

  function bindTranscript(reference: unknown) {
    const next = reference instanceof HTMLElement ? reference : undefined
    if (next === transcript.value) return
    if (transcript.value) detachedPosition = readingPosition()
    transcript.value = next
    if (!next) return
    const position = detachedPosition
    detachedPosition = undefined
    if (!position || position.chatId !== selectedId.value) return
    void nextTick(() => {
      if (transcript.value !== next || selectedId.value !== position.chatId) return
      if (position.focusedId) next.querySelector<HTMLElement>(`#message-${position.focusedId}`)?.focus({ preventScroll: true })
      if (position.atEnd) { next.scrollTop = next.scrollHeight; return }
      const anchor = position.anchor
      const target = anchor && next.querySelector<HTMLElement>(`#message-${anchor.messageId}`)
      if (target && anchor) scrollToReadingAnchor(next, target, anchor.offset, 'instant')
      else next.scrollTop = position.scrollTop
    })
  }

  async function scrollBottom(force = false) {
    const element = transcript.value
    const nearBottom = !element || element.scrollHeight - element.scrollTop - element.clientHeight <= 24
    if (nextAfter.value || (!nearBottom && !force)) return
    await nextTick()
    if (element && element === transcript.value && !nextAfter.value) element.scrollTop = element.scrollHeight
  }

  async function loadMessages(force = false, direction?: 'before' | 'after') {
    const id = selectedId.value
    if (!id) return
    const cursor = direction === 'before' ? nextBefore.value : nextAfter.value
    if (direction && !cursor) return
    if (!direction && !force && nextAfter.value) return
    const previous = requestSignal
    if (force || direction) previous?.abort()
    if (!force && !direction && previous) return
    const controller = new AbortController()
    requestSignal = controller
    loadingPage.value = !!direction
    try {
      const result = await api<MessagePage>(`/chats/${id}/messages${direction ? `?${direction}=${cursor}` : ''}`, { signal: controller.signal })
      if (id !== selectedId.value || controller.signal.aborted) return
      if (direction === 'before') {
        const area = transcript.value
        const anchor = area && readReadingAnchor(area)
        const top = area?.scrollTop ?? 0
        const height = area?.scrollHeight ?? 0
        messages.value = [...result.items.filter(item => !messages.value.some(old => old.id === item.id)), ...messages.value]
        nextBefore.value = result.nextBefore
        if (area) {
          await nextTick()
          if (id !== selectedId.value || transcript.value !== area || controller.signal.aborted) return
          const target = anchor && area.querySelector<HTMLElement>(`#message-${anchor.messageId}`)
          if (anchor && target) scrollToReadingAnchor(area, target, anchor.offset, 'instant')
          else area.scrollTop = top + area.scrollHeight - height
        }
      } else if (direction === 'after') {
        result.items.forEach(updateMessage)
        nextAfter.value = result.nextAfter
        if (!nextAfter.value) offscreenFlow.value = undefined
      }
      else {
        const local = new Map(messages.value.map((message) => [message.id, message]))
        const firstReturned = result.items[0]
        const earlier = firstReturned && !force ? messages.value.filter((item) =>
          item.createdAt < firstReturned.createdAt && !result.items.some((stored) => stored.id === item.id)) : []
        const combined = result.items.map((item) => {
          const live = local.get(item.id)
          if (live && terminal(live) && !terminal(item)) return live
          if (!terminal(item) && live?.content && !item.content) return { ...item, content: live.content }
          return item
        })
        messages.value = [...earlier, ...combined]
        if (force || !nextBefore.value) nextBefore.value = result.nextBefore
        nextAfter.value = null
        offscreenFlow.value = undefined
        if (!result.items.some((item) => !terminal(item))) gap = false
        await scrollBottom(force)
      }
    } catch (reason) {
      if (!controller.signal.aborted) error.value = explain(reason)
    } finally {
      if (requestSignal === controller) { requestSignal = undefined; loadingPage.value = false }
    }
  }

  async function jumpToMessage(messageId: string, offset = 0) {
    const chatId = selectedId.value
    if (!chatId || !/^[\da-f-]{36}$/i.test(messageId)) return false
    requestSignal?.abort()
    if (!messages.value.some(item => item.id === messageId)) {
      const controller = new AbortController()
      requestSignal = controller
      loadingPage.value = true
      try {
        const result = await api<MessagePage>(`/chats/${chatId}/messages?around=${messageId}`, { signal: controller.signal })
        if (controller.signal.aborted || chatId !== selectedId.value) return false
        const active = flow.value
        messages.value = result.items
        nextBefore.value = result.nextBefore
        nextAfter.value = result.nextAfter
        offscreenFlow.value = result.nextAfter && active && !result.items.some(item => item.id === active.id) ? active : undefined
      } catch (reason) {
        if (controller.signal.aborted) return false
        if (reason instanceof ApiError && reason.code === 'invalid_cursor') return false
        throw reason
      } finally {
        if (requestSignal === controller) { requestSignal = undefined; loadingPage.value = false }
      }
    }
    await nextTick()
    if (chatId !== selectedId.value) return false
    const container = transcript.value
    const target = container?.querySelector<HTMLElement>(`#message-${messageId}`)
    if (!container || !target) return false
    const animate = !offset && appearance.value.motion === 'full' && !window.matchMedia('(prefers-reduced-motion: reduce)').matches
    target.focus({ preventScroll: true })
    scrollToReadingAnchor(container, target, offset, animate ? 'smooth' : 'instant')
    clearTimeout(highlightTimer)
    highlightedId.value = messageId
    highlightTimer = setTimeout(() => { highlightedId.value = '' }, 1800)
    return true
  }

  async function openMessage(messageId: string, offset = 0) {
    const currentChat = selectedId.value
    const attempt = ++navigationAttempt
    messageNavigationError.value = ''
    let failure = 'Сообщение недоступно.'
    try {
      if (await jumpToMessage(messageId, offset)) return true
    } catch (reason) { failure = explain(reason) }
    if (currentChat === selectedId.value && attempt === navigationAttempt) messageNavigationError.value = failure
    return false
  }

  async function returnToConversation() {
    navigationAttempt++
    messageNavigationError.value = ''
    if ('at' in route.query || 'offset' in route.query) {
      await router.replace({ query: { ...route.query, at: undefined, offset: undefined } })
    }
    await loadMessages(true)
    transcript.value?.focus({ preventScroll: true })
  }

  async function toggleBookmark(message: Message) {
    const id = selectedId.value
    if (!id || bookmarkPending.value.has(message.id)) return
    bookmarkPending.value.add(message.id)
    const bookmarked = !message.isBookmarked
    try {
      await api(`/chats/${id}/bookmarks/${message.id}`, { method: bookmarked ? 'PUT' : 'DELETE' })
      const current = messages.value.find(item => item.id === message.id)
      if (current && id === selectedId.value) current.isBookmarked = bookmarked
    } catch (reason) { if (id === selectedId.value) error.value = explain(reason) }
    finally { bookmarkPending.value.delete(message.id) }
  }

  async function ensureHub() {
    if (disposed) return
    if (!hub.value) {
      const connection = new HubConnectionBuilder().withUrl('/hubs/chat', { accessTokenFactory: session.hubToken })
        .withAutomaticReconnect([0, 1000, 3000, 7000]).build()
      const current = (id: string) => id === selectedId.value
      connection.on('MessageStarted', (id: string, chat: string) => {
        if (current(chat) && !messages.value.some((item) => item.id === id)) {
          if (nextAfter.value) offscreenFlow.value = newPlaceholder(id)
          else { updateMessage(newPlaceholder(id)); void scrollBottom() }
        }
      })
      connection.on('TokenReceived', (id: string, text: string, offset: number, chat: string) => {
        if (!current(chat)) return
        let item = messages.value.find((message) => message.id === id)
        if (!item && nextAfter.value) {
          if (offscreenFlow.value?.id !== id) offscreenFlow.value = newPlaceholder(id)
          return
        }
        if (!item) { updateMessage(newPlaceholder(id)); item = messages.value.at(-1) }
        if (!item || terminal(item)) return
        if (offset === item.content.length) { item.status = 'streaming'; item.content += text; void scrollBottom() }
        else if (offset > item.content.length) gap = true
      })
      const finish = (message: Message, chat: string) => {
        if (!current(chat)) return
        if (offscreenFlow.value?.id === message.id) offscreenFlow.value = undefined
        if (!nextAfter.value || messages.value.some(item => item.id === message.id)) updateMessage(message)
        gap = false
        void loadMessages()
        void session.reload().catch(() => undefined)
      }
      connection.on('MessageCompleted', (_id: string, _usage: unknown, item: Message, chat: string) => finish(item, chat))
      connection.on('MessageStopped', (_id: string, item: Message, chat: string) => finish(item, chat))
      connection.on('MessageFailed', (_id: string, _error: string, item: Message, chat: string) => finish(item, chat))
      connection.on('SuggestionsReady', (id: string, suggestions: string[], chat: string) => {
        const item = current(chat) ? messages.value.find((message) => message.id === id) : null
        if (item) item.suggestions = suggestions
      })
      connection.onreconnecting(() => { linked.value = false })
      connection.onreconnected(async () => { await join(); await loadMessages() })
      connection.onclose(() => { linked.value = false })
      hub.value = connection
    }
    if (hub.value.state === HubConnectionState.Disconnected && !hubStarting) {
      hubStarting = hub.value.start().finally(() => { hubStarting = undefined })
    }
    try { await hubStarting } catch { linked.value = false; return }
    await join()
  }

  async function join() {
    const id = selectedId.value
    if (!id || hub.value?.state !== HubConnectionState.Connected) return
    try { await hub.value.invoke('JoinChat', id); if (id === selectedId.value) linked.value = true }
    catch { linked.value = false }
  }

  async function create(submittedDraft?: DraftSession) {
    const scope = workspaceQuery.value
    const result = await api<{ id: string }>('/chats', { method: 'POST', body: { workspaceId: workspaceId.value || null } })
    if (submittedDraft) drafts.moveToNewChat(submittedDraft, result.id)
    await loadChats()
    await router.push({ path: `/chats/${result.id}`, query: scope })
    return result.id
  }

  async function newChat() {
    error.value = ''
    try { await create() } catch (reason) { error.value = explain(reason) }
  }

  async function patch(body: Partial<Pick<Chat, 'title' | 'isPinned'>>, id = selectedId.value) {
    if (!id) return false
    error.value = ''
    try {
      await api<void>(`/chats/${id}`, { method: 'PATCH', body })
      await Promise.all([loadChats(), loadProjectChats(), id === selectedId.value ? loadSelected(id) : undefined])
      return true
    }
    catch (reason) { error.value = explain(reason); return false }
  }

  async function move(id: string, workspace: string) {
    const destination = workspace || null
    await api<void>(`/chats/${id}/workspace`, { method: 'PUT', body: { workspaceId: destination } })
    const chat = chats.value.find((item) => item.id === id)
    if (chat) chat.workspaceId = destination
    if (detail.value?.id === id) detail.value.workspaceId = destination

    if (id === selectedId.value) {
      const query = { ...route.query, workspace: destination ?? undefined }
      await router.replace({ path: route.path, query })
    } else {
      await loadProjectChats()
    }
  }

  async function remove(id = selectedId.value) {
    if (!id) return
    try {
      await api<void>(`/chats/${id}`, { method: 'DELETE' })
      if (id === selectedId.value) {
        await router.replace({ path: '/chats', query: workspaceQuery.value })
      }
      await loadChats()
    } catch (reason) { error.value = explain(reason) }
  }

  function changeDraft(value: string) {
    const entry = drafts.current.value
    entry.text = value
    drafts.update(entry)
  }

  function setDraftQuote(quote?: DraftQuote) {
    const entry = drafts.current.value
    entry.quote = quote
    drafts.update(entry)
  }

  function chooseFiles(items: FileList | null) {
    if (!items?.length || busy.value || selected.value?.expiresAt) return
    const entry = drafts.current.value
    const { accepted, rejected } = validateAttachmentSelection(entry.files, Array.from(items), currentPlan.value?.maxAttachmentBytes)
    entry.rejectedFiles = rejected
    if (accepted.length) {
      entry.files = [...entry.files, ...accepted]
      entry.hadFiles = false
      drafts.update(entry)
    }
  }

  function removeFile(index: number) {
    if (busy.value) return
    const entry = drafts.current.value
    const file = entry.files[index]
    if (!file) return
    entry.fileErrors.delete(file)
    entry.files.splice(index, 1)
    drafts.update(entry)
  }

  async function send() {
    const entry = drafts.current.value
    const temporary = Boolean(selected.value?.expiresAt)
    const attachments = temporary ? [] : [...entry.files]
    const content = entry.text
    const quote = entry.quote
    const formattedContent = quotedRequest(content, quote)
    const preferredModel = model.value
    if ((!content.trim() && (quote || !attachments.length)) || busy.value || flow.value) return
    if (submittedLength(formattedContent) > messageLimit) {
      error.value = quote
        ? 'Вопрос и цитата вместе превышают 10 000 символов. Сократите текст перед отправкой.'
        : 'Сообщение превышает 10 000 символов. Сократите текст перед отправкой.'
      return
    }
    error.value = ''; busy.value = true
    const requestId = entry.requestId || (entry.requestId = crypto.randomUUID())
    if (!temporary) entry.fileErrors.clear()
    try {
      const id = selectedId.value || await create(entry)
      if (id === selectedId.value) {
        if (nextAfter.value) await loadMessages(true)
        else await scrollBottom(true)
        await ensureHub()
      }
      const data = new FormData()
      data.set('content', formattedContent)
      data.set('requestId', requestId)
      if (preferredModel) data.set('model', preferredModel)
      attachments.forEach((file) => data.append('attachment', file))
      const answer = await api<{ id: string }>(`/chats/${id}/messages`, { method: 'POST', body: data })
      if (id === selectedId.value && !messages.value.some((item) => item.id === answer.id))
        updateMessage({ ...newPlaceholder(answer.id), status: 'pending' })
      if (entry.text === content && entry.quote === quote) drafts.clear(entry)
      else {
        entry.files = entry.files.filter(file => !attachments.includes(file))
        entry.hadFiles = false
        drafts.update(entry)
      }
      await loadChats()
      if (id === selectedId.value) {
        if (selected.value?.expiresAt) await loadSelected(id)
        await loadMessages()
      }
    } catch (reason) {
      if (reason instanceof ApiError) {
        attachments.forEach((file, index) => {
          const message = reason.fields?.[`attachments[${index}]`]?.[0]
          if (message) entry.fileErrors.set(file, message)
        })
      }
      if (entry === drafts.current.value && (!entry.fileErrors.size || temporary)) error.value = explain(reason)
    }
    finally { busy.value = false }
  }

  async function stop() {
    const item = flow.value
    if (!item) return
    try {
      await ensureHub()
      if (!linked.value) throw new Error('No active hub connection')
      await hub.value?.invoke('StopGeneration', item.id)
      await loadMessages()
    } catch { error.value = 'Не удалось остановить ответ. Дождитесь соединения и повторите.' }
  }

  async function regenerate(target: Message) {
    error.value = ''; busy.value = true
    try {
      await api(`/messages/${target.id}/regenerate`, {
        method: 'POST', body: { requestId: crypto.randomUUID() },
      })
      await loadMessages(true)
    } catch (reason) { error.value = explain(reason) }
    finally { busy.value = false }
  }

  async function readAdaptation() {
    try { adaptation.value = await api<Adaptation | undefined>('/me/personalization/adaptation') }
    catch { /* Feedback remains usable if adaptation is temporarily unavailable. */ }
  }

  async function rate(target: Message, rating: number, reason?: string) {
    try {
      await api<void>(`/messages/${target.id}/feedback`, { method: 'POST', body: { rating, reason } })
      target.rating = rating; target.feedbackReason = reason ?? null
      await readAdaptation()
    } catch (failure) { error.value = explain(failure) }
  }

  async function decide(apply: boolean) {
    if (!adaptation.value) return
    try {
      await api<void>('/me/personalization/adaptation', { method: 'POST', body: { feedbackId: adaptation.value.feedbackId, apply } })
      adaptation.value = undefined
    } catch (reason) { error.value = explain(reason); await readAdaptation() }
  }

  async function download(attachment: Attachment) {
    try {
      const response = await apiResponse(`/attachments/${attachment.id}`)
      const url = URL.createObjectURL(await response.blob())
      const link = document.createElement('a')
      link.href = url; link.download = attachment.fileName; link.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (reason) { error.value = explain(reason) }
  }

  watch(selectedId, async (id) => {
    detachedPosition = undefined
    navigationAttempt++
    messageNavigationError.value = ''
    linked.value = false; gap = false; nextBefore.value = null; messages.value = []; error.value = ''
    requestSignal?.abort()
    requestSignal = undefined
    nextAfter.value = null; loadingPage.value = false; loading.value = false; highlightedId.value = ''; offscreenFlow.value = undefined
    if (!id) return
    loading.value = true
    const anchor = typeof route.query.at === 'string' ? route.query.at : ''
    const offset = Math.max(0, Number(route.query.offset) || 0)
    async function initialPage() {
      if (anchor && await openMessage(anchor, offset)) return
      if (id === selectedId.value) await loadMessages(true)
    }
    await Promise.all([initialPage(), loadSelected(id), ensureHub()])
    if (id === selectedId.value) loading.value = false
  }, { immediate: true })
  watch([selectedId, () => route.query.at, () => route.query.offset], ([id, anchor, rawOffset], [previousId]) => {
    if (id && id === previousId && typeof anchor === 'string') {
      void openMessage(anchor, Math.max(0, Number(rawOffset) || 0))
    }
  })
  watch([workspaceId, selectedId], () => { void loadProjectChats() }, { immediate: true })
  watch(search, (_, __, onCleanup) => {
    const searchTimer = window.setTimeout(() => void loadChats(), 250)
    onCleanup(() => window.clearTimeout(searchTimer))
  })
  onMounted(async () => {
    void api<Persona[]>('/personas').then(items => { personas.value = items }).catch(() => { /* Use the fallback colors. */ })
    timer = setInterval(() => {
      if (flow.value && (!linked.value || gap)) void loadMessages()
      if (flow.value && !linked.value) void ensureHub()
    }, 1500)
    try {
      await Promise.all([loadChats(), readAdaptation(), loadWorkspaces(),
        api<Plan[]>('/plans', { auth: false }).then((value) => { plans.value = value })])
    } catch (reason) { error.value = explain(reason) }
  })
  onUnmounted(() => {
    disposed = true; requestSignal?.abort(); clearInterval(timer); clearTimeout(highlightTimer)
    void hub.value?.stop()
  })

  return { chats, messages, selectedId, selected, model, currentPlan, search, workspaces, workspaceId, workspaceQuery, currentWorkspace,
    projectChats, projectHasMore, projectLoading, loadProjectChats, loadWorkspaces, openProject, projectRemoved,
    nextBefore, nextAfter, loadingPage, highlightedId, messageNavigationError, bookmarkPending, chatsHaveMore, draft, draftQuote,
    draftRestored, draftHadFiles, draftConflict, files, rejectedFiles, fileErrors, busy, loading, error, linked, flow, lastAnswer, adaptation,
    transcript, bindTranscript, readingPosition, personas, ...reading,
    clearDraft: () => drafts.clear(), useExternalDraft: drafts.useExternal, keepOwnDraft: drafts.keepOwn, setDraftQuote,
    loadChats, loadMessages, openMessage, returnToConversation, toggleBookmark, newChat, patch, move, remove, changeDraft, chooseFiles, removeFile, send, stop,
    regenerate, rate, decide, download }
}

export type ChatState = ReturnType<typeof useChat>
