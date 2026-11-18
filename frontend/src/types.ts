export type Usage = {
  planCode: string
  planName: string
  dailyLimit: number | null
  usedToday: number
  remainingPrompts: number | null
}

export type Statistics = { days: number; requests: number; activeUsers: number; promptTokens: number; completionTokens: number
  positiveShare: number | null; meanRating: number | null; usage: Usage | null
  byModel: { label: string; count: number }[]; byPersona: { label: string; count: number }[] }
export type AdminUser = { id: string; username: string; email: string; role: 'Admin' | 'User'; isBlocked: boolean; planCode: string }
export type AdminPersona = { id: string; settings: { profile: PersonaInput; model: string | null; isActive: boolean } }
export type AdminPlan = { id: string; settings: Omit<Plan, 'id'> & { sortOrder: number; isActive: boolean } }

export type Account = {
  id: string
  username: string
  email: string
  role: 'User' | 'Admin'
  onboardingCompleted: boolean
  usage: Usage
}

export type Profile = {
  language: string
  tone: string
  expertiseLevel: string
  responseFormat: string
  responseLength: string
  interests: string[]
  about: string
  autoMemoryEnabled: boolean
  onboardingCompleted: boolean
}

export type PersonaInput = { name: string; description: string; instructions: string; temperature: number }
export type Persona = PersonaInput & { id: string; model: string | null; isCustom: boolean }
export type Chat = { id: string; title: string; personaId: string | null; isPinned: boolean; lastMessageAt: string }
export type Attachment = { id: string; fileName: string; contentType: string; size: number }
export type Message = {
  id: string
  role: 'user' | 'assistant' | 'system'
  content: string
  status: 'completed' | 'pending' | 'streaming' | 'stopped' | 'failed'
  model: string | null
  errorCode: string | null
  suggestions: string[]
  createdAt: string
  triggerMessageId: string | null
  rating: number | null
  feedbackReason: string | null
  attachments?: Attachment[] | null
}
export type MessagePage = { items: Message[]; nextBefore: string | null }
export type Page<T> = { items: T[]; hasMore: boolean }
export type Memory = { id: string; content: string; source: 'manual' | 'auto'; isEnabled: boolean; createdAt: string }
export type Adaptation = { feedbackId: string; currentLength: string; proposedLength: string }
export type Plan = {
  id: string; code: string; name: string; description: string; price: number; durationDays: number
  dailyRequestLimit: number | null; maxPersonas: number; maxMemories: number
  maxAttachmentBytes: number; allowedModels: string[]
}
