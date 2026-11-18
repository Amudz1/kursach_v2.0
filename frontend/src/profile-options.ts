export const languageOptions = { auto: 'Как в запросе', ru: 'Русский', en: 'English' }
export const toneOptions = { friendly: 'Дружелюбный', formal: 'Формальный', concise: 'По делу', detailed: 'Подробный' }
export const expertiseOptions = { beginner: 'Начинающий', intermediate: 'Продвинутый', expert: 'Эксперт' }
export const formatOptions = { text: 'Обычный текст', lists: 'Список', code: 'С примерами кода', steps: 'Пошагово' }
export const lengthOptions = { short: 'Коротко', medium: 'Средне', long: 'Развёрнуто' }

export function labelFor(options: Record<string, string>, value: string) {
  return options[value] ?? value
}
