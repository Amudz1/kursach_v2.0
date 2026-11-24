import { hexContrast } from '../../src/appearance/color'
import type { HexColor } from '../../src/appearance/model'

export type Mode = 'light' | 'dark'
export type ReadingStyle = {
  font: 'inherit' | 'plex' | 'inter' | 'manrope' | 'system'
  scale: 90 | 100 | 110 | 120
  lineHeight: 1.45 | 1.6 | 1.8
  headings: 'serif' | 'body'
  headingColor: 'theme' | 'accent' | 'custom'
  lightHeading: HexColor
  darkHeading: HexColor
}

export const studioColors = {
  light: { paper: '#F7F3EA', ink: '#243248', amber: '#A15E19' },
  dark: { paper: '#18263A', ink: '#F1EDE4', amber: '#F0B66A' },
} as const

export const defaultReadingStyle: ReadingStyle = {
  font: 'inherit',
  scale: 100,
  lineHeight: 1.6,
  headings: 'serif',
  headingColor: 'theme',
  lightHeading: '#784917',
  darkHeading: '#F0B66A',
}

const fontFamilies: Record<ReadingStyle['font'], string> = {
  inherit: 'var(--font-sans)',
  plex: '"IBM Plex Sans", system-ui, sans-serif',
  inter: '"Inter Variable", system-ui, sans-serif',
  manrope: '"Manrope Variable", system-ui, sans-serif',
  system: 'system-ui, sans-serif',
}

export function paperStyles(mode: Mode) {
  const colors = studioColors[mode]
  return {
    '--studio-paper': colors.paper,
    '--studio-ink': colors.ink,
    '--studio-amber': colors.amber,
  }
}

export function readingStyles(style: ReadingStyle, mode: Mode) {
  const palette = studioColors[mode]
  const chosen = mode === 'light' ? style.lightHeading : style.darkHeading
  let color: string = palette.ink
  if (style.headingColor === 'accent') color = palette.amber
  if (style.headingColor === 'custom' && hexContrast(chosen, palette.paper) >= 4.5) color = chosen
  const font = fontFamilies[style.font]
  return {
    '--studio-reading-font': font,
    '--studio-reading-scale': style.scale / 100,
    '--studio-reading-leading': style.lineHeight,
    '--studio-heading-font': style.headings === 'serif' ? 'var(--studio-display)' : font,
    '--studio-heading-color': color,
  }
}
