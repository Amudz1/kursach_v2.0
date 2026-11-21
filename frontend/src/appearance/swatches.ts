import type { HexColor } from './model'

export const fallbackSwatch: HexColor = '#3d5949'
export const legacyFallbackSwatch: HexColor = '#829780'

export const legacySwatches = [
  { value: null, label: 'Стандартный' },
  { value: '#b48965', label: 'Песочный' },
  { value: '#7293b6', label: 'Дымчато-синий' },
  { value: legacyFallbackSwatch, label: 'Шалфей' },
  { value: '#907a9f', label: 'Сливовый' },
]

export const accentSwatches = [
  [fallbackSwatch, 'Хвойный'], ['#183caf', 'Ультрамарин'], ['#456799', 'Дымчато-синий'],
  ['#456556', 'Шалфей'], ['#743d6b', 'Слива'], ['#125b65', 'Бирюза'],
  ['#a34e36', 'Коралл'], ['#78542f', 'Песочный'], ['#9b2b4e', 'Малина'],
  ['#494b85', 'Ирис'], ['#5e652c', 'Олива'], ['#303e54', 'Графит'],
].map(([value, label]) => ({ value, label }))
