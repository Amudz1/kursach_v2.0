import type { Component } from 'vue'
import {
  Atom, BookOpen, Bot, Briefcase, Camera, Code, Coffee, Compass, Feather, FileText, FlaskConical, Folder,
  Gamepad2, Globe, GraduationCap, Heart, Leaf, Lightbulb, Music, Palette, PenLine, Rocket, Sparkles, Star,
  Target, Terminal, Wand, Zap,
} from 'lucide-vue-next'

// Colours and icons of personas and projects (ТЗ 6.4). The server stores the key; the UI maps it.
export const categoryColors = ['cat-1', 'cat-2', 'cat-3', 'cat-4', 'cat-5', 'cat-6', 'cat-7', 'cat-8'] as const
export type CategoryColor = typeof categoryColors[number]

export const categoryLabels: Record<CategoryColor, string> = {
  'cat-1': 'Хвойный', 'cat-2': 'Синий', 'cat-3': 'Охра', 'cat-4': 'Сливовый',
  'cat-5': 'Бирюзовый', 'cat-6': 'Кирпичный', 'cat-7': 'Оливковый', 'cat-8': 'Стальной',
}

export const entityIcons: Record<string, { label: string; icon: Component }> = {
  sparkles: { label: 'Искры', icon: Sparkles },
  bot: { label: 'Робот', icon: Bot },
  code: { label: 'Код', icon: Code },
  terminal: { label: 'Терминал', icon: Terminal },
  book: { label: 'Книга', icon: BookOpen },
  pen: { label: 'Перо', icon: PenLine },
  feather: { label: 'Пёрышко', icon: Feather },
  file: { label: 'Документ', icon: FileText },
  folder: { label: 'Папка', icon: Folder },
  lightbulb: { label: 'Идея', icon: Lightbulb },
  rocket: { label: 'Ракета', icon: Rocket },
  star: { label: 'Звезда', icon: Star },
  heart: { label: 'Сердце', icon: Heart },
  graduation: { label: 'Учёба', icon: GraduationCap },
  briefcase: { label: 'Работа', icon: Briefcase },
  flask: { label: 'Наука', icon: FlaskConical },
  atom: { label: 'Атом', icon: Atom },
  music: { label: 'Музыка', icon: Music },
  camera: { label: 'Фото', icon: Camera },
  palette: { label: 'Палитра', icon: Palette },
  globe: { label: 'Мир', icon: Globe },
  compass: { label: 'Компас', icon: Compass },
  leaf: { label: 'Лист', icon: Leaf },
  zap: { label: 'Молния', icon: Zap },
  coffee: { label: 'Кофе', icon: Coffee },
  gamepad: { label: 'Игры', icon: Gamepad2 },
  target: { label: 'Цель', icon: Target },
  wand: { label: 'Магия', icon: Wand },
}
export const entityIconKeys = Object.keys(entityIcons)

export function isCategoryColor(value: unknown): value is CategoryColor {
  return typeof value === 'string' && (categoryColors as readonly string[]).includes(value)
}

/** FNV-1a; stable across sessions, so an entity without a stored colour always gets the same one. */
export function hashString(value: string) {
  let hash = 0x811c9dc5
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index)
    hash = Math.imul(hash, 0x01000193)
  }
  return hash >>> 0
}

export function categoryFor(seed: string): CategoryColor {
  return categoryColors[hashString(seed) % categoryColors.length]
}

/** The stored colour, or one derived from the id when the server has NULL (ТЗ 9.2). */
export function entityColor(color: string | null | undefined, seed: string): CategoryColor {
  return isCategoryColor(color) ? color : categoryFor(seed)
}

export function entityIcon(icon: string | null | undefined, fallback = 'sparkles'): Component {
  return (icon && entityIcons[icon]?.icon) || entityIcons[fallback].icon
}
