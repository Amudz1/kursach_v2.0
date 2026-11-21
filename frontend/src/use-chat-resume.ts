import { computed, onMounted, onUnmounted, shallowRef, watch, type Ref } from 'vue'
import { api } from './api'
import type { Chat, ResumeChat } from './types'

type Position = { chatId: string; workspaceId: string | null; messageId: string; offset: number; time: number }
type Pending = { owner: string; position: Position }
const key = (owner: string) => `budz.reading.${owner}`
const guid = /^[\da-f-]{36}$/i

function positions(owner: string): Position[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(key(owner)) ?? '[]')
    if (!Array.isArray(value)) return []
    return value.filter((item: Partial<Position> | null) => item && typeof item.chatId === 'string' && guid.test(item.chatId) &&
      typeof item.messageId === 'string' && guid.test(item.messageId) && Number.isFinite(item.offset) && Number.isFinite(item.time))
  } catch { return [] }
}

export function useChatResume(
  owner: Readonly<Ref<string>>, chatId: Readonly<Ref<string>>, workspaceId: Readonly<Ref<string>>,
  selected: Readonly<Ref<Chat | undefined>>, transcript: Readonly<Ref<HTMLElement | undefined>>,
) {
  const resume = shallowRef<ResumeChat>()
  const resumeOffset = shallowRef(0)
  let pending: Pending | undefined
  let timer: number | undefined
  let requested: AbortController | undefined

  function savePosition() {
    window.clearTimeout(timer)
    timer = undefined
    if (!pending) return
    const { owner, position } = pending
    pending = undefined
    try {
      const next = [position, ...positions(owner).filter(item => item.chatId !== position.chatId)].slice(0, 100)
      localStorage.setItem(key(owner), JSON.stringify(next))
    } catch { /* Reading is still available when storage is disabled. */ }
  }

  function rememberPosition() {
    const area = transcript.value
    const chat = selected.value
    if (!owner.value || !area || !chat || chat.expiresAt || chat.id !== chatId.value) return
    const visible = area.getBoundingClientRect()
    const messages = area.querySelectorAll<HTMLElement>('[data-reading-message]')
    // Find the first intersecting message without traversing the whole loaded history on every scroll.
    let low = 0
    let high = messages.length
    while (low < high) {
      const middle = Math.floor((low + high) / 2)
      if (messages[middle].getBoundingClientRect().bottom <= visible.top + 8) low = middle + 1
      else high = middle
    }
    const message = messages[low]
    if (!message || message.getBoundingClientRect().top >= visible.bottom) return
    pending = {
      owner: owner.value,
      position: { chatId: chat.id, workspaceId: chat.workspaceId, messageId: message.dataset.readingMessage!,
        offset: Math.max(0, Math.round(visible.top + 8 - message.getBoundingClientRect().top)), time: Date.now() },
    }
    timer ??= window.setTimeout(savePosition, 350)
  }

  watch([owner, chatId, workspaceId], ([account, active, workspace]) => {
    savePosition()
    requested?.abort()
    resume.value = undefined
    if (!account || active) return
    const nearby = positions(account).find(item => !workspace || item.workspaceId === workspace)
    const query = new URLSearchParams()
    if (workspace) query.set('workspaceId', workspace)
    if (nearby) {
      query.set('chatId', nearby.chatId)
      query.set('messageId', nearby.messageId)
    }
    const request = new AbortController()
    requested = request
    void api<ResumeChat | null>(`/chats/resume?${query}`, { signal: request.signal }).then(value => {
      if (request.signal.aborted) return
      resume.value = value ?? undefined
      resumeOffset.value = nearby && value?.anchorId === nearby.messageId ? nearby.offset : 0
    }).catch(() => { /* The composer and the history remain available if the card cannot load. */ })
  }, { immediate: true })

  onMounted(() => window.addEventListener('pagehide', savePosition))
  onUnmounted(() => {
    requested?.abort()
    window.removeEventListener('pagehide', savePosition)
    savePosition()
  })

  const resumeRoute = computed(() => {
    const item = resume.value
    if (!item) return undefined
    return { path: `/chats/${item.chat.id}`, query: {
      ...(item.chat.workspaceId ? { workspace: item.chat.workspaceId } : {}),
      ...(item.anchorId ? { at: item.anchorId, offset: String(resumeOffset.value) } : {}),
    } }
  })
  return { resume, resumeRoute, rememberPosition }
}
