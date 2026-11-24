import { computed, onMounted, onUnmounted, reactive, shallowRef, watch, type Ref } from 'vue'
import type { Chat } from './types'

export const messageLimit = 10000
export type DraftCaret = { draft: string; offset: number }
export type DraftQuote = { messageId: string; text: string }

export function insertAtCaret(draft: string, transcript: string, caret?: DraftCaret) {
  const text = transcript.trim()
  const offset = caret?.draft === draft ? Math.max(0, Math.min(caret.offset, draft.length)) : draft.length
  const before = draft.slice(0, offset)
  const after = draft.slice(offset)
  const left = before && !/\s$/.test(before) ? ' ' : ''
  const right = after && !/^[\s.,:;!?)}\]]/.test(after) ? ' ' : ''
  const point = before.length + left.length + text.length
  return { draft: before + left + text + right + after, offset: point }
}

type SavedDraft = { text: string; quote?: DraftQuote; hadFiles: boolean; updatedAt: number }
type DraftScope = { owner: string; id: string; persistent: boolean }
type DraftVersion = { raw: string | null; value?: SavedDraft }
export type DraftSession = {
  scope?: DraftScope; text: string; quote?: DraftQuote; files: File[]; rejectedFiles: { name: string; message: string }[]
  fileErrors: Map<File, string>; requestId: string; hadFiles: boolean; restored: boolean; dirty: boolean
  baseline: string | null; loaded: boolean; external?: DraftVersion; timer?: number
}

const prefix = (owner: string) => `budz.draft.${owner}.`
const keyOf = (scope: DraftScope) => prefix(scope.owner) + scope.id
const logoutKey = (owner: string) => `budz.drafts-logout.${owner}`
const retention = 7 * 24 * 60 * 60 * 1000

function readVersion(raw: string | null) {
  if (!raw) return undefined
  try {
    const item = JSON.parse(raw) as Partial<SavedDraft>
    if (!Number.isFinite(item.updatedAt) || item.updatedAt! <= Date.now() - retention || typeof item.text !== 'string') return undefined
    const quote = typeof item.quote?.messageId === 'string' && typeof item.quote.text === 'string' ? item.quote : undefined
    return { text: item.text.slice(0, messageLimit), quote, hadFiles: item.hadFiles === true, updatedAt: item.updatedAt! }
  } catch { return undefined }
}
function empty(scope?: DraftScope): DraftSession {
  return reactive({ scope, text: '', files: [], rejectedFiles: [], fileErrors: new Map(), requestId: '', hadFiles: false,
    restored: false, dirty: false, baseline: null, loaded: false }) as DraftSession
}
function prune(owner: string) {
  try {
    for (let index = localStorage.length - 1; index >= 0; index--) {
      const key = localStorage.key(index)
      if (key?.startsWith(prefix(owner)) && !readVersion(localStorage.getItem(key))) localStorage.removeItem(key)
    }
  } catch { /* Storage may be unavailable. */ }
}
export function clearAccountDrafts(owner: string) {
  const marker = logoutKey(owner)
  try {
    localStorage.setItem(marker, `${Date.now()}-${Math.random()}`)
    for (let index = localStorage.length - 1; index >= 0; index--) {
      const key = localStorage.key(index)
      if (key?.startsWith(prefix(owner))) localStorage.removeItem(key)
    }
  } catch { /* A denied storage operation leaves nothing to clear in this tab. */ }
  window.dispatchEvent(new StorageEvent('storage', { key: marker }))
}

export function useChatDrafts(owner: Readonly<Ref<string>>, chatId: Readonly<Ref<string>>,
  workspaceId: Readonly<Ref<string>>, chat: Readonly<Ref<Chat | undefined>>) {
  const entries = new Map<string, DraftSession>()
  const revokedOwners = new Set<string>()
  const current = shallowRef(empty())
  const scope = computed<DraftScope | undefined>(() => {
    if (!owner.value) return undefined
    if (chatId.value) return { owner: owner.value, id: `chat.${chatId.value}`, persistent: Boolean(chat.value && !chat.value.expiresAt) }
    return { owner: owner.value, id: workspaceId.value ? `new.${workspaceId.value}` : 'new.personal', persistent: true }
  })

  function apply(entry: DraftSession, version?: SavedDraft) {
    entry.text = version?.text ?? ''
    entry.quote = version?.quote
    entry.hadFiles = version?.hadFiles ?? false
    entry.restored = Boolean(version && (entry.text || entry.quote || entry.hadFiles))
    entry.requestId = ''
  }
  function load(entry: DraftSession) {
    if (!entry.scope?.persistent || entry.loaded || revokedOwners.has(entry.scope.owner)) return
    entry.loaded = true
    try {
      const key = keyOf(entry.scope)
      const raw = localStorage.getItem(key)
      const version = readVersion(raw)
      if (raw && !version) localStorage.removeItem(key)
      entry.baseline = version ? raw : null
      if (entry.dirty && version) entry.external = { raw, value: version }
      else if (!entry.dirty) apply(entry, version)
    } catch { /* Leave the current text in memory. */ }
  }
  function write(entry: DraftSession, overwrite = false) {
    window.clearTimeout(entry.timer)
    entry.timer = undefined
    const location = entry.scope
    if (!location?.persistent || !entry.dirty || revokedOwners.has(location.owner) || (entry.external && !overwrite)) return
    try {
      load(entry)
      const key = keyOf(location)
      const raw = localStorage.getItem(key)
      if (!overwrite && raw !== entry.baseline) { entry.external = { raw, value: readVersion(raw) }; return }
      const hasFiles = entry.hadFiles || entry.files.length > 0
      const updated = entry.text || entry.quote || hasFiles
        ? JSON.stringify({ text: entry.text, quote: entry.quote, hadFiles: hasFiles, updatedAt: Date.now() }) : null
      if (updated) localStorage.setItem(key, updated)
      else localStorage.removeItem(key)
      entry.baseline = updated
      entry.external = undefined
      entry.dirty = false
    } catch { /* Leave the current text in memory. */ }
  }
  function update(entry = current.value) {
    entry.requestId = ''
    entry.dirty = true
    window.clearTimeout(entry.timer)
    if (entry.scope?.persistent && !entry.external) entry.timer = window.setTimeout(() => write(entry), 260)
  }
  function clear(entry = current.value) {
    apply(entry)
    entry.files = []
    entry.rejectedFiles = []
    entry.fileErrors.clear()
    update(entry)
    write(entry)
  }
  function useExternal() {
    const entry = current.value
    if (!entry.external) return
    entry.baseline = entry.external.raw
    apply(entry, entry.external.value)
    entry.external = undefined
    entry.dirty = false
  }
  function keepOwn() {
    const entry = current.value
    entry.dirty = true
    write(entry, true)
  }
  function moveToNewChat(entry: DraftSession, id: string) {
    const previous = entry.scope
    if (!previous || previous.owner !== owner.value || revokedOwners.has(previous.owner)) return
    write(entry)
    const original = keyOf(previous)
    const target = { owner: previous.owner, id: `chat.${id}`, persistent: true }
    const nextKey = keyOf(target)
    const oldVersion = entry.baseline
    entries.delete(original)
    entry.scope = target
    entry.loaded = true
    entry.baseline = null
    entry.external = undefined
    entry.dirty = true
    entries.set(nextKey, entry)
    write(entry)
    // Remove the origin only if another tab has not changed it in the meantime.
    try {
      if (localStorage.getItem(original) === oldVersion) localStorage.removeItem(original)
    } catch { /* The moved text is held in this tab either way. */ }
  }
  function onStorage(event: StorageEvent) {
    if (!event.key) return
    const activeOwner = owner.value
    if (activeOwner && event.key === logoutKey(activeOwner)) {
      revokedOwners.add(activeOwner)
      for (const [key, entry] of entries) {
        if (!key.startsWith(prefix(activeOwner))) continue
        window.clearTimeout(entry.timer)
        entries.delete(key)
      }
      current.value = empty()
      return
    }
    const entry = entries.get(event.key)
    if (!entry?.scope?.persistent || !entry.loaded || event.newValue === entry.baseline) return
    entry.external = { raw: event.newValue, value: readVersion(event.newValue) }
  }
  function flushDrafts() {
    for (const entry of entries.values()) write(entry)
  }

  watch(scope, (next) => {
    if (!next || revokedOwners.has(next.owner)) { current.value = empty(); return }
    const key = keyOf(next)
    if (current.value.scope && keyOf(current.value.scope) !== key) write(current.value)
    const entry = entries.get(key) ?? empty(next)
    entry.scope = next
    entries.set(key, entry)
    load(entry)
    current.value = entry
    if (entry.dirty && !entry.external) write(entry)
  }, { immediate: true, flush: 'sync' })
  onMounted(() => {
    window.addEventListener('storage', onStorage)
    window.addEventListener('pagehide', flushDrafts)
    if (owner.value) prune(owner.value)
  })
  onUnmounted(() => {
    window.removeEventListener('storage', onStorage)
    window.removeEventListener('pagehide', flushDrafts)
    flushDrafts()
  })
  return { current, update, clear, useExternal, keepOwn, moveToNewChat }
}
