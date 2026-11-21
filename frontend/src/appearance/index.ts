import { readonly, ref } from 'vue'
import { api } from '../api'
import { applyAppearance } from './apply'
import {
  appearanceSchemaVersion, appearanceStorageKey, defaultAppearance, legacyThemeKey, legacyTintKey, migrateLegacy,
  normalizeAppearance, presetSettings, readCache, removeLegacy, saveAppearance, type AppearanceSettings,
} from './model'

export * from './model'

function storage(): Storage | null {
  try { return window.localStorage } catch { return null }
}

const saved = ref<AppearanceSettings>(loadSaved())
/** What the page shows now: the saved settings, or an unsaved preview on top of them. */
const current = ref<AppearanceSettings>(saved.value)
applyAppearance(current.value)
let owner: string | undefined
let accountRequest = new AbortController()
let writeQueue = Promise.resolve()
const endpoint = '/me/appearance'

interface AppearanceResponse {
  schemaVersion: number
  preset: AppearanceSettings['preset']
  colorMode: AppearanceSettings['mode']
  settings: Partial<AppearanceSettings>
  updatedAt: string | null
}

function loadSaved() {
  try {
    const store = storage()
    return readCache(store?.getItem(appearanceStorageKey) ?? null)?.settings
      ?? migrateLegacy(store?.getItem(legacyThemeKey) ?? null, store?.getItem(legacyTintKey) ?? null)
      ?? presetSettings('classic')
  } catch { return presetSettings('classic') }
}

export const appearance = readonly(current)
export const savedAppearance = readonly(saved)

function applySaved(next: AppearanceSettings, timestamp?: string | null) {
  saved.value = next
  current.value = next
  applyAppearance(next)
  const store = storage()
  if (store) saveAppearance(store, next, timestamp ? new Date(timestamp) : undefined, owner)
}

function fromServer(view: AppearanceResponse): AppearanceSettings {
  const preset = normalizeAppearance({ preset: view.preset }).preset
  return normalizeAppearance({ ...presetSettings(preset), ...view.settings, preset, mode: view.colorMode })
}

export function appearanceDocument(next: AppearanceSettings) {
  const { preset, mode: colorMode, ...settings } = next
  const baseline = presetSettings(preset)
  const differences = Object.fromEntries(Object.entries(settings).filter(([key, value]) =>
    value !== baseline[key as keyof AppearanceSettings]))
  return { schemaVersion: appearanceSchemaVersion, preset, colorMode, settings: differences }
}

function queueWrite<T>(callback: (signal: AbortSignal) => Promise<T>) {
  const signal = accountRequest.signal
  const result = writeQueue.then(() => {
    signal.throwIfAborted()
    return callback(signal)
  })
  writeQueue = result.then(() => undefined, () => undefined)
  return result
}

export async function saveAccountAppearance(next: AppearanceSettings) {
  if (!owner) { applySaved(next); return }
  await queueWrite(async (signal) => {
    const view = await api<AppearanceResponse>(endpoint, { method: 'PUT', body: appearanceDocument(next), signal })
    if (!signal.aborted) applySaved(fromServer(view), view.updatedAt)
  })
}

/** Updates a quick setting immediately and sends it to the active account. */
export function setAppearance(patch: Partial<AppearanceSettings>) {
  const next = normalizeAppearance({ ...saved.value, ...patch })
  applySaved(next)
  if (!owner) return Promise.resolve()
  return queueWrite((signal) => api<AppearanceResponse>(endpoint, { method: 'PUT', body: appearanceDocument(next), signal }))
    .then(() => undefined)
}

export async function syncAppearanceAccount(userId: string) {
  if (userId === owner) return
  accountRequest.abort()
  accountRequest = new AbortController()
  owner = userId
  const { signal } = accountRequest
  const store = storage()
  let cached: ReturnType<typeof readCache> = null
  let legacy: AppearanceSettings | null = null
  try {
    cached = readCache(store?.getItem(appearanceStorageKey) ?? null)
    legacy = migrateLegacy(store?.getItem(legacyThemeKey) ?? null, store?.getItem(legacyTintKey) ?? null)
  } catch { /* The browser may forbid storage. */ }
  const migration = !cached?.owner ? legacy ?? cached?.settings : null
  const fallback = cached?.owner === userId ? cached.settings : migration ?? presetSettings('classic')
  saved.value = current.value = fallback
  applyAppearance(fallback)
  try {
    let view = await api<AppearanceResponse>(endpoint, { signal })
    if (signal.aborted) return
    if (migration && view.updatedAt === null) {
      view = await api<AppearanceResponse>(endpoint, {
        method: 'PUT', body: { ...appearanceDocument(migration), onlyIfMissing: true }, signal,
      })
    }
    if (signal.aborted) return
    applySaved(fromServer(view), view.updatedAt)
    if (store) removeLegacy(store)
  } catch { /* Keep the matching local cache when the server is unavailable. */ }
}

export function clearAppearanceAccount() {
  if (!owner) return
  const previous = owner
  accountRequest.abort()
  accountRequest = new AbortController()
  owner = undefined
  try {
    const store = storage()
    if (readCache(store?.getItem(appearanceStorageKey) ?? null)?.owner === previous) store?.removeItem(appearanceStorageKey)
  } catch { /* The browser may forbid storage. */ }
  saved.value = current.value = presetSettings('classic')
  applyAppearance(current.value)
}

/** Shows a change without saving it; `cancelPreview` or the next `setAppearance` ends it. */
export function previewAppearance(patch: Partial<AppearanceSettings>) {
  current.value = normalizeAppearance({ ...saved.value, ...patch })
  applyAppearance(current.value)
}

export function cancelPreview() {
  current.value = saved.value
  applyAppearance(saved.value)
}

export async function resetAppearance() {
  if (!owner) { applySaved(presetSettings('classic')); return }
  await queueWrite(async (signal) => {
    await api(endpoint, { method: 'DELETE', signal })
    if (!signal.aborted) applySaved(presetSettings('classic'))
  })
}

window.addEventListener('storage', (event) => {
  if (event.storageArea !== storage()) return
  if (event.key !== appearanceStorageKey && event.key !== null) return
  const cache = readCache(event.newValue)
  if (cache?.owner !== owner) return
  const next = cache?.settings ?? normalizeAppearance(defaultAppearance)
  saved.value = next
  current.value = next
  applyAppearance(next)
})
