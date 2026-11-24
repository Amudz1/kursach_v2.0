import { describe, expect, it } from 'vitest'
import { categoryColors, categoryFor, entityColor, entityIcon, entityIcons, hashString, isCategoryColor } from './entities'

describe('entity colours', () => {
  it('hashes the same seed to the same category', () => {
    expect(hashString('Анна')).toBe(hashString('Анна'))
    expect(categoryFor('workspace-42')).toBe(categoryFor('workspace-42'))
  })

  it('spreads seeds over several categories', () => {
    const used = new Set(Array.from({ length: 64 }, (_, index) => categoryFor(`seed-${index}`)))
    expect(used.size).toBeGreaterThanOrEqual(6)
    for (const color of used) expect(categoryColors).toContain(color)
  })

  it('prefers an explicit valid colour', () => {
    expect(entityColor('cat-5', 'x')).toBe('cat-5')
    expect(entityColor('#ff0000', 'x')).toBe(categoryFor('x'))
    expect(isCategoryColor('cat-9')).toBe(false)
  })

  it('falls back to the default icon for unknown keys', () => {
    expect(entityIcon('code')).toBe(entityIcons.code.icon)
    expect(entityIcon('nope')).toBe(entityIcons.sparkles.icon)
    expect(entityIcon(null)).toBe(entityIcons.sparkles.icon)
  })
})
