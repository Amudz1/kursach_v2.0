export type SupportMessage = { id: number; content: string; fromSupport: boolean; createdAt: string }
export type SupportThread = { userId: string; username: string; lastMessageAt: string }
export type SupportHistory = { messages: SupportMessage[]; previous: number | null }
export type SupportQueue = { threads: SupportThread[]; previous: number | null }

export const supportTime = new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short', timeStyle: 'short' })
