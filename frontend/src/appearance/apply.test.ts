import { describe, expect, it } from 'vitest'
import { applyAppearance } from './apply'
import { normalizeAppearance } from './model'

describe('applyAppearance', () => {
  it('updates theme attributes and removes obsolete values without changing unrelated styles', () => {
    const attributes = new Map([['data-other', 'keep']])
    const properties = new Map([['--app-test', 'keep']])
    const root = {
      setAttribute: (key: string, value: string) => attributes.set(key, value),
      getAttribute: (key: string) => attributes.get(key),
      removeAttribute: (key: string) => attributes.delete(key),
      style: {
        setProperty: (key: string, value: string) => properties.set(key, value),
        removeProperty: (key: string) => properties.delete(key),
      },
    } as unknown as HTMLElement
    applyAppearance(normalizeAppearance({
      preset: 'mint', mode: 'dark', accent: '#123456', accent2: 'auto',
      assistantAvatar: 'custom', assistantAvatarColor: '#fefefe',
    }), root)
    expect(attributes.get('data-theme')).toBe('dark')
    expect(attributes.get('data-preset')).toBe('mint')
    expect(properties.get('--theme-tint')).toBe('#123456')
    expect(properties.get('--avatar-custom-text')).toBe('#000000')

    applyAppearance(normalizeAppearance({}), root)
    expect(attributes.has('data-theme')).toBe(false)
    expect(attributes.has('data-tint')).toBe(false)
    expect(attributes.has('data-accent-auto')).toBe(false)
    expect(properties.has('--theme-tint')).toBe(false)
    expect(properties.has('--avatar-custom-text')).toBe(false)
    expect(attributes.get('data-other')).toBe('keep')
    expect(properties.get('--app-test')).toBe('keep')
  })
})
