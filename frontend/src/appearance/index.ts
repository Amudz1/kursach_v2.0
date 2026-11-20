import { readonly, ref } from 'vue'
import { applyAppearance } from './apply'
import {
  appearanceStorageKey, defaultAppearance, loadAppearance, normalizeAppearance, readCache, saveAppearance,
  type AppearanceSettings,
} from './model'

export * from './model'

function storage(): Storage | null {
  try { return window.localStorage } catch { return null }
}

const saved = ref<AppearanceSettings>(loadSaved())
/** What the page shows now: the saved settings, or an unsaved preview on top of them. */
const current = ref<AppearanceSettings>(saved.value)
applyAppearance(current.value)

function loadSaved() {
  const store = storage()
  return store ? loadAppearance(store) : normalizeAppearance(defaultAppearance)
}

export const appearance = readonly(current)
export const savedAppearance = readonly(saved)

/** Saves a change and applies it to every open tab. */
export function setAppearance(patch: Partial<AppearanceSettings>) {
  const next = normalizeAppearance({ ...saved.value, ...patch })
  saved.value = next
  current.value = next
  applyAppearance(next)
  const store = storage()
  if (store) saveAppearance(store, next)
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

export function resetAppearance() {
  setAppearance({ ...defaultAppearance })
}

window.addEventListener('storage', (event) => {
  if (event.storageArea !== storage()) return
  if (event.key !== appearanceStorageKey && event.key !== null) return
  const next = readCache(event.newValue)?.settings ?? normalizeAppearance(defaultAppearance)
  saved.value = next
  current.value = next
  applyAppearance(next)
})
