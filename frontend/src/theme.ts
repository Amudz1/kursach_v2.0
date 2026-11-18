import { readonly, ref } from 'vue'

const themeKey = 'budz.theme'
const tintKey = 'budz.theme-tint'
const root = document.documentElement
export type Theme = 'system' | 'light' | 'dark'

function readTheme(value: string | null | undefined): Theme {
  return value === 'light' || value === 'dark' ? value : 'system'
}

function readTint(value: string | null | undefined) {
  return value && /^#[0-9a-f]{6}$/i.test(value) ? value.toLowerCase() : null
}

const preference = ref<Theme>(readTheme(root.dataset.theme))
const tint = ref(readTint(root.dataset.tint))
export const theme = readonly(preference)
export const themeTint = readonly(tint)

function applyTheme(value: Theme) {
  preference.value = value
  if (value === 'system') delete root.dataset.theme
  else root.dataset.theme = value
}

export function setTheme(value: Theme) {
  applyTheme(value)
  try { localStorage.setItem(themeKey, value) } catch { /* Keep the choice for this page. */ }
}

function applyTint(value: string | null) {
  tint.value = value
  if (value) {
    root.dataset.tint = value
    root.style.setProperty('--theme-tint', value)
  } else {
    delete root.dataset.tint
    root.style.removeProperty('--theme-tint')
  }
}

export function setThemeTint(value: string | null, preview = false) {
  const next = readTint(value)
  applyTint(next)
  if (preview) return
  try {
    if (next) localStorage.setItem(tintKey, next)
    else localStorage.removeItem(tintKey)
  } catch { /* Keep the choice for this page. */ }
}

window.addEventListener('storage', (event) => {
  if (event.storageArea !== localStorage) return
  if (event.key === themeKey || event.key === null) applyTheme(readTheme(event.newValue))
  if (event.key === tintKey || event.key === null) applyTint(readTint(event.newValue))
})
