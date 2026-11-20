import { describe, expect, it } from 'vitest'
import { adjustForContrast, autoSecondAccent, hexContrast, oklchToRgb, parseCssColor, parseHex, readableOn, rgbToOklch, toHex } from './color'

describe('hex parsing', () => {
  it('parses six-digit hex with or without the hash', () => {
    expect(parseHex('#ff8000')).toEqual({ r: 255, g: 128, b: 0 })
    expect(parseHex('ff8000')).toEqual({ r: 255, g: 128, b: 0 })
    expect(parseHex('#fff')).toBeNull()
  })

  it('round-trips through toHex', () => {
    expect(toHex(parseHex('#3d5949')!)).toBe('#3d5949')
  })
})

describe('contrast', () => {
  it('matches the WCAG reference values', () => {
    expect(hexContrast('#000000', '#ffffff')).toBeCloseTo(21, 1)
    expect(hexContrast('#777777', '#ffffff')).toBeCloseTo(4.48, 1)
    expect(hexContrast('#ffffff', '#ffffff')).toBeCloseTo(1, 5)
  })

  it('moves a colour until it reaches the target contrast', () => {
    const adjusted = adjustForContrast('#9fd3c7', '#ffffff')
    expect(adjusted).not.toBeNull()
    expect(hexContrast(adjusted!, '#ffffff')).toBeGreaterThanOrEqual(4.5)
  })

  it('keeps a colour that already passes', () => {
    expect(adjustForContrast('#1d3b2d', '#ffffff')).toBe('#1d3b2d')
  })

  it('picks readable text for a fill', () => {
    expect(readableOn('#1d3b2d')).toBe('#ffffff')
    expect(readableOn('#f6e7a1')).toBe('#172329')
  })
})

describe('OKLCH', () => {
  it('round-trips sRGB colours within one step per channel', () => {
    for (const hex of ['#3d5949', '#e9ada8', '#1b2a6b', '#ffcc00']) {
      const back = oklchToRgb(rgbToOklch(parseHex(hex)!))
      const original = parseHex(hex)!
      expect(Math.abs(back.r - original.r)).toBeLessThanOrEqual(1)
      expect(Math.abs(back.g - original.g)).toBeLessThanOrEqual(1)
      expect(Math.abs(back.b - original.b)).toBeLessThanOrEqual(1)
    }
  })

  it('derives a different second accent', () => {
    const second = autoSecondAccent('#3d5949')
    expect(second).toMatch(/^#[0-9a-f]{6}$/)
    expect(second).not.toBe('#3d5949')
  })
})

describe('parseCssColor', () => {
  it('reads computed rgb() and color(srgb) values', () => {
    expect(parseCssColor('rgb(10, 20, 30)')).toEqual({ r: 10, g: 20, b: 30 })
    expect(parseCssColor('color(srgb 1 0 0.5)')).toEqual({ r: 255, g: 0, b: 128 })
    expect(parseCssColor('transparent')).toBeNull()
  })
})
