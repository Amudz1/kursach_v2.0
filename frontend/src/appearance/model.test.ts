import { describe, expect, it } from 'vitest'
import {
  appearanceStorageKey, defaultAppearance, legacyThemeKey, legacyTintKey, loadAppearance, migrateLegacy,
  normalizeAppearance, readCache, toRootAttributes,
} from './model'

function memoryStorage(initial: Record<string, string> = {}) {
  const data = new Map(Object.entries(initial))
  return {
    data,
    getItem: (key: string) => data.get(key) ?? null,
    setItem: (key: string, value: string) => { data.set(key, value) },
    removeItem: (key: string) => { data.delete(key) },
  }
}

describe('normalizeAppearance', () => {
  it('returns defaults for anything that is not an object', () => {
    expect(normalizeAppearance(null)).toEqual(defaultAppearance)
    expect(normalizeAppearance('dark')).toEqual(defaultAppearance)
  })

  it('keeps valid values and replaces invalid ones field by field', () => {
    const settings = normalizeAppearance({ mode: 'dark', preset: 'neon', radius: 'pill', fontScale: 130, accent: '#ABCDEF', accent2: 'red' })
    expect(settings.mode).toBe('dark')
    expect(settings.preset).toBe('classic')
    expect(settings.radius).toBe('pill')
    expect(settings.fontScale).toBe(100)
    expect(settings.accent).toBe('#abcdef')
    expect(settings.accent2).toBeNull()
  })

  it('clamps and rounds the background tint', () => {
    expect(normalizeAppearance({ backgroundTint: 99 }).backgroundTint).toBe(30)
    expect(normalizeAppearance({ backgroundTint: -4 }).backgroundTint).toBe(0)
    expect(normalizeAppearance({ backgroundTint: 12.6 }).backgroundTint).toBe(13)
    expect(normalizeAppearance({ backgroundTint: Number.NaN }).backgroundTint).toBe(defaultAppearance.backgroundTint)
  })

  it('accepts auto as the second accent', () => {
    expect(normalizeAppearance({ accent2: 'auto' }).accent2).toBe('auto')
  })

  it('derives an absent or invalid outline from the preset and keeps explicit choices', () => {
    expect(normalizeAppearance({ preset: 'studio' }).messageOutline).toBe('wave')
    expect(normalizeAppearance({ preset: 'studio', messageOutline: 'unknown' }).messageOutline).toBe('wave')
    expect(normalizeAppearance({ preset: 'classic' }).messageOutline).toBe('rectangle')
    const selected = normalizeAppearance({ preset: 'studio', messageOutline: 'rectangle' })
    expect(toRootAttributes(selected).dataset['message-outline']).toBe('rectangle')
  })
})

describe('toRootAttributes', () => {
  it('omits data-theme for the system mode', () => {
    expect(toRootAttributes(normalizeAppearance({})).dataset.theme).toBeUndefined()
    expect(toRootAttributes(normalizeAppearance({ mode: 'light' })).dataset.theme).toBe('light')
  })

  it('sets the tint attribute and variable only for a custom accent', () => {
    const plain = toRootAttributes(normalizeAppearance({}))
    expect(plain.dataset.tint).toBeUndefined()
    expect(plain.vars['theme-tint']).toBeUndefined()
    const tinted = toRootAttributes(normalizeAppearance({ accent: '#336699' }))
    expect(tinted.dataset.tint).toBe('#336699')
    expect(tinted.vars['theme-tint']).toBe('#336699')
  })

  it('uses the custom avatar colour only with the custom avatar', () => {
    expect(toRootAttributes(normalizeAppearance({ assistantAvatarColor: '#112233' })).vars['avatar-custom']).toBeUndefined()
    expect(toRootAttributes(normalizeAppearance({ assistantAvatar: 'custom', assistantAvatarColor: '#112233' })).vars['avatar-custom']).toBe('#112233')
  })

  it('writes the font scale as a string', () => {
    expect(toRootAttributes(normalizeAppearance({ fontScale: 120 })).dataset['font-scale']).toBe('120')
  })
})

describe('migrateLegacy', () => {
  it('returns null when neither dev_2 key is set', () => {
    expect(migrateLegacy(null, null)).toBeNull()
    expect(migrateLegacy('purple', 'nope')).toBeNull()
  })

  it('maps the theme and tint', () => {
    const settings = migrateLegacy('dark', '#3D5949')
    expect(settings?.mode).toBe('dark')
    expect(settings?.accent).toBe('#3d5949')
  })
})

describe('loadAppearance', () => {
  const now = new Date('2026-01-01T00:00:00Z')

  it('moves the dev_2 keys into the new cache', () => {
    const storage = memoryStorage({ [legacyThemeKey]: 'light', [legacyTintKey]: '#aa5500' })
    const settings = loadAppearance(storage, now)
    expect(settings.mode).toBe('light')
    expect(storage.data.has(legacyThemeKey)).toBe(false)
    expect(storage.data.has(legacyTintKey)).toBe(false)
    expect(readCache(storage.getItem(appearanceStorageKey))?.settings.accent).toBe('#aa5500')
  })

  it('prefers the new cache over the old keys', () => {
    const storage = memoryStorage({ [legacyThemeKey]: 'light' })
    storage.setItem(appearanceStorageKey, JSON.stringify({ version: 1, updatedAt: now.toISOString(), settings: { mode: 'dark' } }))
    expect(loadAppearance(storage, now).mode).toBe('dark')
    expect(storage.data.has(legacyThemeKey)).toBe(false)
  })

  it('falls back to defaults on broken JSON or storage errors', () => {
    expect(loadAppearance(memoryStorage({ [appearanceStorageKey]: '{oops' }), now)).toEqual(defaultAppearance)
    const failing = { getItem: () => { throw new Error('denied') }, setItem: () => {}, removeItem: () => {} }
    expect(loadAppearance(failing, now)).toEqual(defaultAppearance)
  })
})
