// Appearance settings: the single list of keys, allowed values and defaults (ТЗ 6.2).
// Pure functions only, so the rules can be unit-tested and mirrored by the server validator.
import { readableOn } from './color'

export const appearanceSchemaVersion = 1
export const appearanceStorageKey = 'budz.appearance'
export const legacyThemeKey = 'budz.theme'
export const legacyTintKey = 'budz.theme-tint'

export const appearanceOptions = {
  mode: ['system', 'light', 'dark'],
  preset: ['studio', 'classic', 'ultramarine', 'graphite', 'midnight', 'mint', 'sunset', 'terminal', 'contrast'],
  glow: ['off', 'soft', 'bright'],
  radius: ['sharp', 'standard', 'soft', 'pill'],
  density: ['compact', 'normal', 'spacious'],
  font: ['system', 'inter', 'manrope', 'plex'],
  fontScale: [90, 100, 110, 120],
  codeFont: ['system', 'jetbrains', 'fira'],
  messageStyle: ['bubbles', 'cards', 'flat'],
  chatWidth: ['narrow', 'medium', 'wide'],
  userSide: ['left', 'right'],
  assistantAvatar: ['persona', 'brand', 'custom'],
  codeTheme: ['classic', 'github', 'atom-one', 'tokyo-night'],
  messageActions: ['always', 'hover'],
  pattern: ['none', 'dots', 'grid', 'noise', 'mesh'],
  motion: ['full', 'reduced', 'off'],
} as const

type Options = typeof appearanceOptions
type OptionKey = keyof Options
export type ColorMode = Options['mode'][number]
export type PresetId = Options['preset'][number]
export type HexColor = `#${string}`

export type AppearanceSettings = { [K in OptionKey]: Options[K][number] } & {
  /** Main accent; `null` keeps the preset colour. */
  accent: HexColor | null
  /** Second accent; `auto` derives it from the main accent on the hue wheel. */
  accent2: HexColor | 'auto' | null
  /** Strength of the accent mixed into backgrounds, 0–30 %. */
  backgroundTint: number
  /** Used only when `assistantAvatar` is `custom`. */
  assistantAvatarColor: HexColor | null
}

export const backgroundTintRange = { min: 0, max: 30 } as const

export const defaultAppearance: Readonly<AppearanceSettings> = Object.freeze({
  mode: 'system',
  preset: 'classic',
  accent: null,
  accent2: null,
  backgroundTint: 11,
  glow: 'soft',
  radius: 'standard',
  density: 'normal',
  font: 'system',
  fontScale: 100,
  codeFont: 'system',
  messageStyle: 'cards',
  chatWidth: 'medium',
  userSide: 'left',
  assistantAvatar: 'brand',
  assistantAvatarColor: null,
  codeTheme: 'classic',
  messageActions: 'always',
  pattern: 'none',
  motion: 'full',
})

export const presets: Record<PresetId, { label: string; options?: Partial<AppearanceSettings> }> = {
  studio: { label: 'Студия', options: { font: 'plex', messageStyle: 'flat', userSide: 'right' } },
  classic: { label: 'Классическая' },
  ultramarine: { label: 'Ультрамарин' },
  graphite: { label: 'Графит', options: { mode: 'dark' } },
  midnight: { label: 'Полночь', options: { mode: 'dark', glow: 'bright' } },
  mint: { label: 'Мята', options: { mode: 'light' } },
  sunset: { label: 'Закат' },
  terminal: { label: 'Терминал', options: { mode: 'dark', codeFont: 'jetbrains' } },
  contrast: { label: 'Высокий контраст', options: { glow: 'off' } },
}

export function presetSettings(preset: PresetId): AppearanceSettings {
  return { ...defaultAppearance, preset, ...presets[preset].options }
}

const hexPattern = /^#[0-9a-f]{6}$/i

export function readHex(value: unknown): HexColor | null {
  return typeof value === 'string' && hexPattern.test(value) ? value.toLowerCase() as HexColor : null
}

function readOption<K extends OptionKey>(key: K, value: unknown): Options[K][number] {
  const allowed = appearanceOptions[key] as readonly unknown[]
  return (allowed.includes(value) ? value : defaultAppearance[key]) as Options[K][number]
}

function readTint(value: unknown) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return defaultAppearance.backgroundTint
  return Math.min(backgroundTintRange.max, Math.max(backgroundTintRange.min, Math.round(value)))
}

/** Turns any stored or received value into complete, valid settings; unknown or invalid fields fall back to defaults. */
export function normalizeAppearance(input: unknown): AppearanceSettings {
  const source = typeof input === 'object' && input !== null ? input as Record<string, unknown> : {}
  const settings = {} as Record<string, unknown>
  for (const key of Object.keys(appearanceOptions) as OptionKey[]) settings[key] = readOption(key, source[key])
  settings.accent = readHex(source.accent)
  settings.accent2 = source.accent2 === 'auto' ? 'auto' : readHex(source.accent2)
  settings.backgroundTint = readTint(source.backgroundTint)
  settings.assistantAvatarColor = readHex(source.assistantAvatarColor)
  return settings as AppearanceSettings
}

export interface RootAttributes {
  /** `data-*` attribute names without the prefix; a missing key means the attribute is removed. */
  dataset: Record<string, string>
  /** Custom properties without the leading `--`. */
  vars: Record<string, string>
}

/** Every attribute and property the engine may set, so stale ones can be removed. */
export const managedAttributes = [
  'theme', 'tint', 'preset', 'glow', 'radius', 'density', 'font', 'font-scale', 'code-font', 'message-style',
  'chat-width', 'user-side', 'avatar', 'code-theme', 'message-actions', 'pattern', 'motion', 'accent-auto', 'background-tinted',
] as const
export const managedVars = ['theme-tint', 'accent-2-custom', 'avatar-custom', 'avatar-custom-text', 'background-tint'] as const

export function toRootAttributes(settings: AppearanceSettings): RootAttributes {
  const dataset: Record<string, string> = {
    preset: settings.preset,
    glow: settings.glow,
    radius: settings.radius,
    density: settings.density,
    font: settings.font,
    'font-scale': String(settings.fontScale),
    'code-font': settings.codeFont,
    'message-style': settings.messageStyle,
    'chat-width': settings.chatWidth,
    'user-side': settings.userSide,
    avatar: settings.assistantAvatar,
    'code-theme': settings.codeTheme,
    'message-actions': settings.messageActions,
    pattern: settings.pattern,
    motion: settings.motion,
  }
  // `data-theme` and `data-tint` keep the dev_2 names, which the Classic theme CSS already reads.
  if (settings.mode !== 'system') dataset.theme = settings.mode
  const vars: Record<string, string> = { 'background-tint': `${settings.backgroundTint}%` }
  if (settings.accent) {
    dataset.tint = settings.accent
    vars['theme-tint'] = settings.accent
  }
  if (settings.accent2 && settings.accent2 !== 'auto') vars['accent-2-custom'] = settings.accent2
  if (settings.accent2 === 'auto') dataset['accent-auto'] = 'true'
  if (settings.backgroundTint !== defaultAppearance.backgroundTint) dataset['background-tinted'] = 'true'
  if (settings.assistantAvatar === 'custom' && settings.assistantAvatarColor) {
    vars['avatar-custom'] = settings.assistantAvatarColor
    vars['avatar-custom-text'] = readableOn(settings.assistantAvatarColor, '#ffffff', '#000000')
  }
  return { dataset, vars }
}

export interface AppearanceCache {
  version: number
  updatedAt: string
  settings: AppearanceSettings
  /** Precomputed attributes the inline script in index.html applies before Vue mounts. */
  root: RootAttributes
  owner?: string
}

type StorageLike = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>

export function createCache(settings: AppearanceSettings, now = new Date()): AppearanceCache {
  return { version: appearanceSchemaVersion, updatedAt: now.toISOString(), settings, root: toRootAttributes(settings) }
}

export function readCache(raw: string | null): { settings: AppearanceSettings; updatedAt: string | null; owner?: string } | null {
  if (!raw) return null
  try {
    const value = JSON.parse(raw) as Partial<AppearanceCache> | null
    if (!value || typeof value !== 'object') return null
    const updatedAt = typeof value.updatedAt === 'string' && !Number.isNaN(Date.parse(value.updatedAt)) ? value.updatedAt : null
    return { settings: normalizeAppearance(value.settings), updatedAt, owner: typeof value.owner === 'string' ? value.owner : undefined }
  } catch {
    return null
  }
}

/** Maps the dev_2 keys `budz.theme` and `budz.theme-tint` to the new settings; `null` when neither is set. */
export function migrateLegacy(theme: string | null, tint: string | null): AppearanceSettings | null {
  const mode = theme === 'light' || theme === 'dark' || theme === 'system' ? theme : null
  const accent = readHex(tint)
  if (!mode && !accent) return null
  return normalizeAppearance({ ...defaultAppearance, mode: mode ?? 'system', accent })
}

/**
 * Reads cached settings, moving the old dev_2 keys into the new cache on first run.
 * Storage errors (private mode, quota) are ignored: the defaults are used for this page.
 */
export function loadAppearance(storage: StorageLike, now = new Date()): AppearanceSettings {
  try {
    const cached = readCache(storage.getItem(appearanceStorageKey))
    const legacy = migrateLegacy(storage.getItem(legacyThemeKey), storage.getItem(legacyTintKey))
    if (cached) {
      if (legacy) removeLegacy(storage)
      return cached.settings
    }
    if (!legacy) return normalizeAppearance(defaultAppearance)
    storage.setItem(appearanceStorageKey, JSON.stringify(createCache(legacy, now)))
    removeLegacy(storage)
    return legacy
  } catch {
    return normalizeAppearance(defaultAppearance)
  }
}

export function removeLegacy(storage: StorageLike) {
  storage.removeItem(legacyThemeKey)
  storage.removeItem(legacyTintKey)
}

export function saveAppearance(storage: StorageLike, settings: AppearanceSettings, now = new Date(), owner?: string) {
  try {
    storage.setItem(appearanceStorageKey, JSON.stringify({ ...createCache(settings, now), owner }))
  } catch { /* Keep the choice for this page. */ }
}
