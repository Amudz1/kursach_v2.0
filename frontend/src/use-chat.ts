import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { api, apiResponse, explain } from './api'
import { useSession } from './session'
import type { Adaptation, Attachment, Chat, Message, MessagePage, Page, Persona, Plan, Profile } from './types'

const terminal = (message: Message) => message.status !== 'pending' && message.status !== 'streaming'

export function useChat() {
  const route = useRoute()
  const router = useRouter()
  const session = useSession()
  const selectedId = computed(() => typeof route.params.id === 'string' ? route.params.id : '')
  const chats = ref<Chat[]>([])
  const messages = ref<Message[]>([])
  const selected = computed(() => chats.value.find((chat) => chat.id === selectedId.value))
  const personas = ref<Persona[]>([])
  const profile = ref<Profile>()
  const plans = ref<Plan[]>([])
  const currentPlan = computed(() => plans.value.find((plan) => plan.code === session.account?.usage.planCode))
  const personaId = ref('')
  const model = ref('')
  const search = ref('')
  const nextBefore = ref<string | null>(null)
  const chatsHaveMore = ref(false)
  const page = ref(1)
  const draft = ref('')
  const files = ref<File[]>([])
  const busy = ref(false)
  const loading = ref(false)
  const error = ref('')
  const linked = ref(false)
  const adaptation = ref<Adaptation>()
  const hub = shallowRef<HubConnection>()
  const flow = computed(() => messages.value.findLast((message) => !terminal(message)))
  const lastUser = computed(() => messages.value.findLast((message) => message.role === 'user'))
  const lastAnswer = computed(() => messages.value.findLast((message) => message.role === 'assistant'))
  const transcript = ref<HTMLElement>()
  let requestSignal: AbortController | undefined
  let hubStarting: Promise<void> | undefined
  let timer: ReturnType<typeof setInterval> | undefined
  let pendingRequest = ''
  let disposed = false
  let gap = false

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

  function updateMessage(item: Message) {
    const old = messages.value.find((message) => message.id === item.id)
    if (old) Object.assign(old, item)
    else messages.value.push(item)
  }

  function newPlaceholder(id: string): Message {
    return { id, role: 'assistant', content: '', status: 'streaming', model: model.value || null, errorCode: null,
      suggestions: [], createdAt: new Date().toISOString(), triggerMessageId: null, rating: null, feedbackReason: null }
  }

  async function scrollBottom(force = false) {
    const element = transcript.value
    const nearBottom = !element || element.scrollHeight - element.scrollTop - element.clientHeight < 210
    if (!nearBottom && !force) return
    await nextTick()
    if (element) element.scrollTop = element.scrollHeight
  }

  async function loadMessages(force = false, earlier = false) {
    const id = selectedId.value
    if (!id) return
    const before = earlier ? nextBefore.value : null
    if (earlier && !before) return
    const previous = requestSignal
    if (force || earlier) previous?.abort()
    if (!force && !earlier && previous) return
    const controller = new AbortController()
    requestSignal = controller
    try {
      const result = await api<MessagePage>(`/chats/${id}/messages${before ? `?before=${before}` : ''}`, { signal: controller.signal })
      if (id !== selectedId.value || controller.signal.aborted) return
      if (earlier) messages.value = [...result.items, ...messages.value]
      else {
        const local = new Map(messages.value.map((message) => [message.id, message]))
        const firstReturned = result.items[0]
        const earlier = firstReturned && !force ? messages.value.filter((item) =>
          item.createdAt < firstReturned.createdAt && !result.items.some((stored) => stored.id === item.id)) : []
        const combined = result.items.map((item) => {
          const live = local.get(item.id)
          if (!terminal(item) && live?.content && !item.content) return { ...item, content: live.content }
          return item
        })
        messages.value = [...earlier, ...combined]
        if (!result.items.some((item) => !terminal(item))) gap = false
        await scrollBottom(force)
      }
      if (earlier || force || !nextBefore.value) nextBefore.value = result.nextBefore
    } catch (reason) {
      if (!controller.signal.aborted) error.value = explain(reason)
    } finally {
      if (requestSignal === controller) requestSignal = undefined
    }
  }

  async function ensureHub() {
    if (disposed) return
    if (!hub.value) {
      const connection = new HubConnectionBuilder().withUrl('/hubs/chat', { accessTokenFactory: session.hubToken })
        .withAutomaticReconnect([0, 1000, 3000, 7000]).build()
      const current = (id: string) => id === selectedId.value
      connection.on('MessageStarted', (id: string, chat: string) => {
        if (current(chat) && !messages.value.some((item) => item.id === id)) {
          updateMessage(newPlaceholder(id)); void scrollBottom(true)
        }
      })
      connection.on('TokenReceived', (id: string, text: string, offset: number, chat: string) => {
        if (!current(chat)) return
        let item = messages.value.find((message) => message.id === id)
        if (!item) { updateMessage(newPlaceholder(id)); item = messages.value.at(-1) }
        if (!item || terminal(item)) return
        if (offset === item.content.length) { item.status = 'streaming'; item.content += text; void scrollBottom() }
        else if (offset > item.content.length) gap = true
      })
      const finish = (message: Message, chat: string) => {
        if (!current(chat)) return
        updateMessage(message); gap = false
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

  async function create() {
    const result = await api<{ id: string }>('/chats', { method: 'POST', body: { personaId: personaId.value || null } })
    await loadChats()
    await router.push(`/chats/${result.id}`)
    return result.id
  }

  async function newChat() {
    error.value = ''
    try { await create() } catch (reason) { error.value = explain(reason) }
  }

  async function changePersona(value: string) {
    personaId.value = value
    if (!selectedId.value || !value) return
    await patch({ personaId: value })
  }

  async function patch(body: Partial<Pick<Chat, 'title' | 'isPinned' | 'personaId'>>) {
    const id = selectedId.value
    if (!id) return false
    error.value = ''
    try { await api<void>(`/chats/${id}`, { method: 'PATCH', body }); await loadChats(); return true }
    catch (reason) { error.value = explain(reason); return false }
  }

  async function remove() {
    const id = selectedId.value
    if (!id) return
    try {
      await api<void>(`/chats/${id}`, { method: 'DELETE' })
      await router.replace('/chats')
      await loadChats()
    } catch (reason) { error.value = explain(reason) }
  }

  function changeDraft(value: string) {
    draft.value = value
    pendingRequest = ''
  }

  function chooseFiles(items: FileList | null) {
    if (!items) return
    if (files.value.length + items.length > 4) { error.value = 'Можно прикрепить до четырёх файлов.'; return }
    files.value = [...files.value, ...Array.from(items)]
    pendingRequest = ''
  }

  function removeFile(index: number) {
    files.value.splice(index, 1)
    pendingRequest = ''
  }

  async function send() {
    if ((!draft.value.trim() && !files.value.length) || busy.value || flow.value) return
    error.value = ''; busy.value = true
    if (!pendingRequest) pendingRequest = crypto.randomUUID()
    try {
      const id = selectedId.value || await create()
      if (id === selectedId.value) await ensureHub()
      const data = new FormData()
      data.set('content', draft.value)
      data.set('requestId', pendingRequest)
      if (model.value) data.set('model', model.value)
      files.value.forEach((file) => data.append('attachment', file))
      const answer = await api<{ id: string }>(`/chats/${id}/messages`, { method: 'POST', body: data })
      if (id === selectedId.value && !messages.value.some((item) => item.id === answer.id))
        updateMessage({ ...newPlaceholder(answer.id), status: 'pending' })
      draft.value = ''; files.value = []; pendingRequest = ''
      await loadChats()
      await loadMessages()
      await scrollBottom(true)
    } catch (reason) { error.value = explain(reason) }
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

  async function revise(target: Message, text?: string) {
    error.value = ''; busy.value = true
    try {
      const path = `/messages/${target.id}${text === undefined ? '/regenerate' : ''}`
      await api(path, { method: text === undefined ? 'POST' : 'PATCH',
        body: { requestId: crypto.randomUUID(), content: text } })
      await loadMessages(true)
      return true
    } catch (reason) { error.value = explain(reason); return false }
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
      if (apply) profile.value = await api<Profile>('/me/personalization')
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
    linked.value = false; gap = false; nextBefore.value = null; messages.value = []; error.value = ''
    requestSignal?.abort()
    if (!id) return
    personaId.value = selected.value?.personaId ?? personas.value[0]?.id ?? ''
    loading.value = true
    await Promise.all([loadMessages(true), ensureHub()])
    if (id === selectedId.value) loading.value = false
  }, { immediate: true })
  watch(selected, (chat) => { if (chat?.personaId) personaId.value = chat.personaId })
  onMounted(async () => {
    timer = setInterval(() => {
      if (flow.value && (!linked.value || gap)) void loadMessages()
      if (flow.value && !linked.value) void ensureHub()
    }, 1500)
    try {
      await Promise.all([loadChats(), readAdaptation(),
        api<Persona[]>('/personas').then((items) => { personas.value = items; if (!personaId.value) personaId.value = items[0]?.id ?? '' }),
        api<Profile>('/me/personalization').then((value) => { profile.value = value }),
        api<Plan[]>('/plans', { auth: false }).then((value) => { plans.value = value })])
    } catch (reason) { error.value = explain(reason) }
  })
  onUnmounted(() => {
    disposed = true; requestSignal?.abort(); clearInterval(timer)
    void hub.value?.stop()
  })

  return { session, chats, messages, selectedId, selected, personaId, personas, profile, model, currentPlan, search,
    nextBefore, chatsHaveMore, draft, files, busy, loading, error, linked, flow, lastUser, lastAnswer, adaptation, transcript,
    loadChats, loadMessages, newChat, changePersona, patch, remove, changeDraft, chooseFiles, removeFile, send, stop,
    revise, rate, decide, download }
}
