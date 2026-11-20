// Compatibility layer for the dev_2 theme API; the settings now live in `appearance/`.
import { computed } from 'vue'
import { appearance, previewAppearance, readHex, setAppearance, type ColorMode } from './appearance'

export type Theme = ColorMode

export const theme = computed(() => appearance.value.mode)
export const themeTint = computed(() => appearance.value.accent)

export function setTheme(value: Theme) {
  setAppearance({ mode: value })
}

export function setThemeTint(value: string | null, preview = false) {
  const accent = readHex(value)
  if (preview) previewAppearance({ accent })
  else setAppearance({ accent })
}
