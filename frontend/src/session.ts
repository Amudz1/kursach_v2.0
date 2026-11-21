import { ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError, api, useSessionInApi } from './api'
import { clearAppearanceAccount, syncAppearanceAccount } from './appearance'
import type { Account } from './types'

type Grant = { accessToken: string; expiresAt: string }

export const useSession = defineStore('session', () => {
  const startedAt = Date.now()
  const account = ref<Account | null>(null)
  const access = ref<string | null>(null)
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
    const next = await api<Account>('/me')
    if (next.id !== account.value?.id) await syncAppearanceAccount(next.id)
    account.value = next
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
    await api<void>('/auth/logout', { method: 'POST', auth: false })
    clear()
  }

  async function hubToken() {
    if (access.value && Date.now() + 15000 < expires) return access.value
    return await refresh() ?? ''
  }

  return { access, account, ready, reload, signIn, signOut, clear, hubToken, startedAt }
})
