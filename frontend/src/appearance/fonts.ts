import type { AppearanceSettings } from './model'

// Fonts are bundled locally and loaded only when chosen (ТЗ 4.3, 12): each import becomes its own CSS chunk.
const loaders: Record<string, () => Promise<unknown>> = {
  inter: () => import('@fontsource-variable/inter'),
  manrope: () => import('@fontsource-variable/manrope'),
  plex: () => Promise.all([
    import('@fontsource/ibm-plex-sans/400.css'),
    import('@fontsource/ibm-plex-sans/500.css'),
    import('@fontsource/ibm-plex-sans/600.css'),
    import('@fontsource/ibm-plex-sans/700.css'),
  ]),
  jetbrains: () => import('@fontsource-variable/jetbrains-mono'),
  fira: () => import('@fontsource-variable/fira-code'),
}

const loaded = new Set<string>()

function load(name: string) {
  const loader = loaders[name]
  if (!loader || loaded.has(name)) return
  loaded.add(name)
  // A failed chunk only means the fallback font stays; let a later choice retry.
  loader().catch(() => loaded.delete(name))
}

export function loadFonts(settings: Pick<AppearanceSettings, 'font' | 'codeFont'>) {
  load(settings.font)
  load(settings.codeFont)
}
