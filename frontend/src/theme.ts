import { readonly, ref } from 'vue'

const storageKey = 'budz.theme'
export type Theme = 'system' | 'light' | 'dark'

function readTheme(value: string | null | undefined): Theme {
  return value === 'light' || value === 'dark' ? value : 'system'
}

const preference = ref<Theme>(readTheme(document.documentElement.dataset.theme))
export const theme = readonly(preference)

function applyTheme(value: Theme) {
  preference.value = value
  if (value === 'system') delete document.documentElement.dataset.theme
  else document.documentElement.dataset.theme = value
}

export function setTheme(value: Theme) {
  applyTheme(value)
  try { localStorage.setItem(storageKey, value) } catch { /* Keep the choice for this page. */ }
}

window.addEventListener('storage', (event) => {
  if (event.key === storageKey || event.key === null) applyTheme(readTheme(event.newValue))
})
