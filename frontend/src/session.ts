import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError, api, useSessionInApi } from './api'
import { clearAppearanceAccount, syncAppearanceAccount } from './appearance'
import { clearAccountDrafts } from './chat-drafts'
import type { Account, InterfaceVariant } from './types'

type Grant = { accessToken: string; expiresAt: string }
const normalizeInterfaceVariant = (value: unknown): InterfaceVariant => value === 'focus' ? 'focus' : 'current'

function cacheInterfaceVariant(owner: string, variant: InterfaceVariant) {
  try {
    const key = `budz.interface.${owner}`
    if (localStorage.getItem(key) !== variant) localStorage.setItem(key, variant)
  } catch { /* The server value remains available when storage is disabled. */ }
}

export const useSession = defineStore('session', () => {
  const startedAt = Date.now()
  const account = ref<Account | null>(null)
  const access = ref<string | null>(null)
  const interfaceSaving = ref(false)
  const currentInterfaceForSession = ref(false)
  const savedInterfaceVariant = computed(() => normalizeInterfaceVariant(account.value?.interfaceVariant))
  const interfaceVariant = computed(() => currentInterfaceForSession.value ? 'current' : savedInterfaceVariant.value)
  let sessionVersion = 0
  let interfaceVersion = 0
  let expires = 0
  let refreshing: Promise<string | null> | null = null
  let initializing: Promise<void> | null = null
  let initialized = false

  function accept(grant: Grant) {
    access.value = grant.accessToken
    expires = Date.parse(grant.expiresAt)
  }

  function clear() {
    clearAppearanceAccount()
    sessionVersion++
    currentInterfaceForSession.value = false
    interfaceSaving.value = false
    access.value = null
    account.value = null
    expires = 0
  }

  async function rotate() {
    try {
      const grant = await api<Grant>('/auth/refresh', { method: 'POST', auth: false })
      accept(grant)
      return grant.accessToken
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) clear()
      return null
    }
  }

  function refresh(): Promise<string | null> {
    if (refreshing) return refreshing
    const work = navigator.locks ? navigator.locks.request('budz-session-refresh', rotate) : rotate()
    refreshing = work.then<string | null>((result) => result)
      .finally(() => { refreshing = null })
    return refreshing
  }

  useSessionInApi(() => access.value, refresh)

  async function reload() {
    const version = sessionVersion
    const readVersion = interfaceVersion
    const next = await api<Account>('/me')
    if (version !== sessionVersion) return
    const sameAccount = next.id === account.value?.id
    if (!sameAccount) await syncAppearanceAccount(next.id)
    if (version !== sessionVersion) return
    // A request started before a successful save must not overwrite that save.
    const sameOwner = next.id === account.value?.id
    const variant = sameOwner && readVersion !== interfaceVersion
      ? savedInterfaceVariant.value : normalizeInterfaceVariant(next.interfaceVariant)
    if (!sameOwner) currentInterfaceForSession.value = false
    account.value = { ...next, interfaceVariant: variant }
    cacheInterfaceVariant(next.id, variant)
  }

  async function setInterfaceVariant(value: InterfaceVariant) {
    const owner = account.value?.id
    if (!owner) throw new ApiError(401, 'account_required', 'Войдите, чтобы изменить интерфейс.')
    if (interfaceSaving.value) throw new ApiError(409, 'interface_saving', 'Дождитесь сохранения интерфейса.')
    const version = sessionVersion
    interfaceSaving.value = true
    try {
      await api<void>('/me/interface', { method: 'PUT', body: { interfaceVariant: value } })
      if (version !== sessionVersion || account.value?.id !== owner) return
      interfaceVersion++
      account.value = { ...account.value, interfaceVariant: value }
      currentInterfaceForSession.value = false
      cacheInterfaceVariant(owner, value)
    } finally {
      if (version === sessionVersion) interfaceSaving.value = false
    }
  }

  function useCurrentInterfaceForSession() {
    currentInterfaceForSession.value = true
  }

  function ready() {
    if (initialized) return Promise.resolve()
    if (!initializing) {
      initializing = (async () => {
        if (await refresh()) await reload()
      })().catch(() => clear()).finally(() => { initialized = true })
    }
    return initializing
  }

  async function signIn(path: 'login' | 'register', body: object) {
    accept(await api<Grant>(`/auth/${path}`, { method: 'POST', body, auth: false }))
    await reload()
  }

  async function signOut() {
    const owner = account.value?.id
    await api<void>('/auth/logout', { method: 'POST', auth: false })
    if (owner) clearAccountDrafts(owner)
    clear()
  }

  async function hubToken() {
    if (access.value && Date.now() + 15000 < expires) return access.value
    return await refresh() ?? ''
  }

  return { access, account, ready, reload, signIn, signOut, clear, hubToken, startedAt,
    savedInterfaceVariant, interfaceVariant, interfaceSaving, setInterfaceVariant, useCurrentInterfaceForSession }
})
