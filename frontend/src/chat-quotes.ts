import type { DraftQuote } from './chat-drafts'

export const quoteLimit = 600

export function submittedLength(content: string) {
  // Multipart form encoding expands line breaks to CRLF before the server counts them.
  return content.trim().replace(/\r\n|\r|\n/g, '\r\n').length
}

export function quotedRequest(question: string, quote?: DraftQuote) {
  if (!quote) return question
  const blockquote = quote.text.trim().split(/\r?\n/).map(line => `> ${line}`).join('\n')
  return `${blockquote}\n\n${question.trim()}`
}

export function quotedSelection(content: HTMLElement, selected: Selection | null) {
  if (!selected || selected.isCollapsed || !selected.rangeCount) return ''
  const range = selected.getRangeAt(0).cloneRange()
  if (!range.intersectsNode(content)) return ''
  if (!content.contains(range.startContainer)) range.setStart(content, 0)
  if (!content.contains(range.endContainer)) range.setEnd(content, content.childNodes.length)
  const fragment = range.cloneContents()
  fragment.querySelectorAll('.code-toolbar, .code-gutter').forEach(element => element.remove())
  return fragment.textContent?.trim() ?? ''
}
