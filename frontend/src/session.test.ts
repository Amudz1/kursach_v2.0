import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { api } from './api'
import { useSession } from './session'

vi.mock('./api', () => ({ api: vi.fn(), useSessionInApi: vi.fn(), ApiError: Error }))
vi.mock('./appearance', () => ({ clearAppearanceAccount: vi.fn(), syncAppearanceAccount: vi.fn() }))

const cached = new Map<string, string>()
const request = vi.mocked(api)
const account = (id: string, interfaceVariant?: string) => ({ id, interfaceVariant })

function deferred<T>() {
  let resolve!: (result: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { resolve, promise }
}

beforeEach(() => {
  setActivePinia(createPinia())
  request.mockReset()
  cached.clear()
  vi.stubGlobal('localStorage', { getItem: (key: string) => cached.get(key), setItem: (key: string, value: string) => cached.set(key, value) })
})
afterEach(() => vi.unstubAllGlobals())

it('uses a confirmed server preference, including defaults for older or unknown values', async () => {
  const session = useSession()
  cached.set('budz.interface.owner', 'focus')
  request.mockResolvedValueOnce(account('owner'))
  await session.reload()
  expect(session.interfaceVariant).toBe('current')
  expect(cached.get('budz.interface.owner')).toBe('current')

  request.mockResolvedValueOnce(account('owner', 'unknown'))
  await session.reload()
  expect(session.interfaceVariant).toBe('current')

  request.mockResolvedValueOnce(account('other', 'focus'))
  await session.reload()
  expect(session.interfaceVariant).toBe('focus')
  expect(cached.get('budz.interface.other')).toBe('focus')
  expect(cached.get('budz.interface.owner')).toBe('current')
})

it('retains the active layout after a failed save and applies a confirmed retry', async () => {
  const session = useSession()
  request.mockResolvedValueOnce(account('owner', 'current'))
  await session.reload()
  request.mockRejectedValueOnce(new Error('Сеть недоступна'))
  await expect(session.setInterfaceVariant('focus')).rejects.toThrow('Сеть недоступна')
  expect(session.interfaceVariant).toBe('current')
  expect(cached.get('budz.interface.owner')).toBe('current')

  const save = deferred<void>()
  request.mockReturnValueOnce(save.promise)
  const updating = session.setInterfaceVariant('focus')
  expect(session.interfaceSaving).toBe(true)
  expect(session.interfaceVariant).toBe('current')
  save.resolve(undefined)
  await updating
  expect(request).toHaveBeenLastCalledWith('/me/interface', { method: 'PUT', body: { interfaceVariant: 'focus' } })
  expect(session.interfaceVariant).toBe('focus')
  expect(cached.get('budz.interface.owner')).toBe('focus')
})

it('does not overwrite a new layout with a stale account response or a temporary fallback', async () => {
  const session = useSession()
  request.mockResolvedValueOnce(account('owner', 'current'))
  await session.reload()
  const stale = deferred<ReturnType<typeof account>>()
  request.mockReturnValueOnce(stale.promise)
  const reading = session.reload()
  request.mockResolvedValueOnce(undefined)
  await session.setInterfaceVariant('focus')
  stale.resolve(account('owner', 'current'))
  await reading
  expect(session.interfaceVariant).toBe('focus')

  session.useCurrentInterfaceForSession()
  request.mockResolvedValueOnce(account('owner', 'focus'))
  await session.reload()
  expect(session.interfaceVariant).toBe('current')
  expect(session.savedInterfaceVariant).toBe('focus')
  expect(cached.get('budz.interface.owner')).toBe('focus')
  request.mockResolvedValueOnce(undefined)
  await session.setInterfaceVariant('focus')
  expect(session.interfaceVariant).toBe('focus')
})
