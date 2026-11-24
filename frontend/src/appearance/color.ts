// Colour maths for custom colours: WCAG contrast and OKLCH corrections (ТЗ 6.5).
// Pure functions, no DOM, so they can be unit-tested.

export type Rgb = { r: number; g: number; b: number }
export type Oklch = { l: number; c: number; h: number }

export function parseHex(hex: string): Rgb | null {
  const match = /^#?([0-9a-f]{6})$/i.exec(hex.trim())
  if (!match) return null
  const value = parseInt(match[1], 16)
  return { r: (value >> 16) & 255, g: (value >> 8) & 255, b: value & 255 }
}

export function toHex({ r, g, b }: Rgb): `#${string}` {
  const part = (value: number) => Math.round(Math.min(255, Math.max(0, value))).toString(16).padStart(2, '0')
  return `#${part(r)}${part(g)}${part(b)}`
}

const toLinear = (channel: number) => {
  const value = channel / 255
  return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
}
const fromLinear = (value: number) => 255 * (value <= 0.0031308 ? 12.92 * value : 1.055 * value ** (1 / 2.4) - 0.055)

export function luminance(color: Rgb) {
  return 0.2126 * toLinear(color.r) + 0.7152 * toLinear(color.g) + 0.0722 * toLinear(color.b)
}

/** WCAG 2 contrast ratio, 1–21. */
export function contrastRatio(a: Rgb, b: Rgb) {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (light + 0.05) / (dark + 0.05)
}

export function hexContrast(a: string, b: string) {
  const left = parseHex(a), right = parseHex(b)
  return left && right ? contrastRatio(left, right) : 1
}

export function rgbToOklch(color: Rgb): Oklch {
  const r = toLinear(color.r), g = toLinear(color.g), b = toLinear(color.b)
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)
  const L = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s
  const A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s
  const B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s
  const hue = (Math.atan2(B, A) * 180) / Math.PI
  return { l: L, c: Math.hypot(A, B), h: hue < 0 ? hue + 360 : hue }
}

function oklchToLinear({ l: L, c, h }: Oklch) {
  const A = c * Math.cos((h * Math.PI) / 180), B = c * Math.sin((h * Math.PI) / 180)
  const l = (L + 0.3963377774 * A + 0.2158037573 * B) ** 3
  const m = (L - 0.1055613458 * A - 0.0638541728 * B) ** 3
  const s = (L - 0.0894841775 * A - 1.291485548 * B) ** 3
  return [
    4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
    -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
    -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s,
  ]
}

/** Converts to sRGB, lowering chroma until the colour fits the gamut, so the hue stays recognisable. */
export function oklchToRgb(color: Oklch): Rgb {
  let chroma = color.c
  for (let step = 0; step < 24; step++) {
    const channels = oklchToLinear({ ...color, c: chroma })
    if (channels.every((value) => value >= -0.0005 && value <= 1.0005) || chroma < 0.001) {
      const [r, g, b] = channels.map((value) => fromLinear(Math.min(1, Math.max(0, value))))
      return { r, g, b }
    }
    chroma *= 0.85
  }
  const [r, g, b] = oklchToLinear({ ...color, c: 0 }).map((value) => fromLinear(Math.min(1, Math.max(0, value))))
  return { r, g, b }
}

/**
 * Moves the OKLCH lightness of `color` away from `against` until the contrast reaches `target`.
 * Returns the original colour when it already passes, or `null` when no lightness passes.
 */
export function adjustForContrast(color: string, against: string, target = 4.5): `#${string}` | null {
  const source = parseHex(color), backdrop = parseHex(against)
  if (!source || !backdrop) return null
  if (contrastRatio(source, backdrop) >= target) return toHex(source)
  const base = rgbToOklch(source)
  const darken = luminance(backdrop) > 0.18
  for (let step = 1; step <= 100; step++) {
    const l = darken ? base.l - step * 0.01 : base.l + step * 0.01
    if (l < 0 || l > 1) break
    const candidate = oklchToRgb({ ...base, l })
    if (contrastRatio(candidate, backdrop) >= target) return toHex(candidate)
  }
  return null
}

/** Second accent for «авто»: the main accent rotated on the hue wheel with the same lightness and chroma. */
export function autoSecondAccent(accent: string, rotation = 150): `#${string}` | null {
  const source = parseHex(accent)
  if (!source) return null
  const base = rgbToOklch(source)
  return toHex(oklchToRgb({ ...base, h: (base.h + rotation) % 360, c: Math.max(base.c, 0.06) }))
}

/** Text colour with the higher contrast on a filled background. */
export function readableOn(fill: string, light = '#ffffff', dark = '#172329'): string {
  return hexContrast(fill, light) >= hexContrast(fill, dark) ? light : dark
}

/** Parses `rgb()` / `rgba()` and `color(srgb …)` strings as returned by getComputedStyle. */
export function parseCssColor(value: string): Rgb | null {
  const rgb = /^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)/i.exec(value)
  if (rgb) return { r: +rgb[1], g: +rgb[2], b: +rgb[3] }
  const srgb = /^color\(srgb\s+([\d.e-]+)\s+([\d.e-]+)\s+([\d.e-]+)/i.exec(value)
  if (srgb) return { r: Math.round(+srgb[1] * 255), g: Math.round(+srgb[2] * 255), b: Math.round(+srgb[3] * 255) }
  return parseHex(value)
}
