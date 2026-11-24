import { effectScope, nextTick } from 'vue'
import { afterEach, expect, it, vi } from 'vitest'
import { useChat } from './use-chat'

const mocks = vi.hoisted(() => {
  const chatId = 'a0000000-0000-4000-8000-000000000001'
  const messageId = 'a0000000-0000-4000-8000-000000000002'
  const chat = { id: chatId, workspaceId: null, title: 'Тестовый диалог' }
  const message = { id: messageId, role: 'assistant', status: 'streaming', content: 'Часть ответа' }
  const hub = {
    state: 'Disconnected', start: vi.fn(async () => { hub.state = 'Connected' }), stop: vi.fn(), invoke: vi.fn(),
    on: vi.fn(), onreconnecting: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(),
  }
  return { chat, message, hub, mounted: [] as Array<() => unknown>, unmounted: [] as Array<() => unknown>, api: vi.fn() }
})

vi.mock('vue', async importOriginal => ({
  ...await importOriginal<typeof import('vue')>(),
  onMounted: (callback: () => unknown) => { mocks.mounted.push(callback) },
  onUnmounted: (callback: () => unknown) => { mocks.unmounted.push(callback) },
}))
vi.mock('vue-router', () => ({ useRoute: () => ({ params: { id: mocks.chat.id }, query: {} }), useRouter: () => ({}) }))
vi.mock('./api', () => ({ api: mocks.api, ApiError: Error, explain: String }))
vi.mock('./appearance', () => ({ appearance: { value: { motion: 'reduced' } } }))
vi.mock('./session', () => ({ useSession: () => ({ account: { id: 'owner' }, hubToken: vi.fn() }) }))
vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    build() { return mocks.hub }
  },
}))

class Transcript {
  scrollHeight = 900
  clientHeight = 200
  scrollTop = 350

  constructor(private readonly messageTop: number) {}

  getBoundingClientRect() { return { top: 100, bottom: 300 } }
  querySelectorAll() { return [this.querySelector()] }
  querySelector() {
    return { dataset: { readingMessage: mocks.message.id }, offsetHeight: 100,
      getBoundingClientRect: () => ({ top: this.messageTop, bottom: this.messageTop + 100 }) }
  }
  scrollTo({ top }: { top: number }) { this.scrollTop = top }
}

afterEach(() => vi.unstubAllGlobals())

it('keeps the draft, active answer and reading anchor when the presentation attaches a new transcript', async () => {
  vi.stubGlobal('HTMLElement', Transcript)
  vi.stubGlobal('window', { addEventListener: vi.fn(), removeEventListener: vi.fn(), setTimeout, clearTimeout })
  mocks.api.mockImplementation(async (path: string) => {
    if (path.startsWith('/chats?')) return { items: [mocks.chat], hasMore: false }
    if (path === `/chats/${mocks.chat.id}`) return mocks.chat
    if (path === `/chats/${mocks.chat.id}/messages`) return { items: [mocks.message], nextBefore: null, nextAfter: null }
    return []
  })

  const scope = effectScope()
  const chat = scope.run(useChat)!
  try {
    await Promise.all(mocks.mounted.map(callback => callback()))
    await vi.waitFor(() => expect(chat.linked.value).toBe(true))

    const first = new Transcript(82)
    chat.bindTranscript(first)
    chat.changeDraft('Незаконченный вопрос')
    const attachment = new File(['пример'], 'notes.txt')
    chat.chooseFiles([attachment] as unknown as FileList)
    expect(chat.readingPosition()?.anchor).toEqual({ messageId: mocks.message.id, offset: 26 })

    chat.bindTranscript(null)
    expect(chat.readingPosition()?.anchor?.messageId).toBe(mocks.message.id)
    const second = new Transcript(160)
    second.scrollTop = 0
    chat.bindTranscript(second)
    await nextTick()

    expect(second.scrollTop).toBe(78)
    expect(chat.selectedId.value).toBe(mocks.chat.id)
    expect(chat.draft.value).toBe('Незаконченный вопрос')
    expect(chat.files.value).toEqual([attachment])
    expect(chat.flow.value?.id).toBe(mocks.message.id)
    expect(mocks.hub.start).toHaveBeenCalledTimes(1)
    expect(mocks.hub.invoke).toHaveBeenCalledExactlyOnceWith('JoinChat', mocks.chat.id)
    expect(mocks.api.mock.calls.filter(([path]) => path === `/chats/${mocks.chat.id}/messages`)).toHaveLength(1)
    expect(mocks.api.mock.calls.some(([, options]) => options?.method === 'POST')).toBe(false)
  } finally {
    scope.stop()
    for (const callback of mocks.unmounted) callback()
  }
})
