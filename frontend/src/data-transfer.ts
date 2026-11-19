import { apiResponse } from './api'

export async function exportConversations(format: 'json' | 'markdown', scope: { workspaceId?: string; chatId?: string; includeAccount?: boolean } = {}) {
  const params = new URLSearchParams({ format })
  if (scope.workspaceId) params.set('workspaceId', scope.workspaceId)
  if (scope.chatId) params.set('chatId', scope.chatId)
  if (scope.includeAccount) params.set('includeAccount', 'true')
  const result = await apiResponse(`/data/export?${params}`)
  const url = URL.createObjectURL(await result.blob())
  const link = document.createElement('a')
  link.href = url
  const name = scope.chatId ? 'budz-dialogue' : scope.workspaceId ? 'budz-workspace' : 'budz-data'
  link.download = `${name}.${format === 'json' ? 'json' : 'md'}`
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
