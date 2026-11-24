import { readonly, ref } from 'vue'

// Short confirmations (ТЗ 8). Form errors stay inline next to the field.
export type ToastTone = 'success' | 'info' | 'warning' | 'danger'
export interface Toast { id: number; tone: ToastTone; text: string; action?: { label: string; run: () => void } }

const list = ref<Toast[]>([])
let next = 1
const timers = new Map<number, number>()

export function dismissToast(id: number) {
  window.clearTimeout(timers.get(id))
  timers.delete(id)
  list.value = list.value.filter((toast) => toast.id !== id)
}

export function showToast(text: string, tone: ToastTone = 'success', options: { action?: Toast['action']; duration?: number } = {}) {
  const id = next++
  list.value = [...list.value.slice(-3), { id, tone, text, action: options.action }]
  // Errors stay a little longer; any toast with an action waits for a reader to act on it.
  const duration = options.duration ?? (options.action ? 8000 : tone === 'danger' ? 6000 : 3500)
  timers.set(id, window.setTimeout(() => dismissToast(id), duration))
  return id
}

export function useToast() {
  return {
    toasts: readonly(list),
    show: showToast,
    success: (text: string) => showToast(text, 'success'),
    info: (text: string) => showToast(text, 'info'),
    warning: (text: string) => showToast(text, 'warning'),
    error: (text: string) => showToast(text, 'danger'),
    dismiss: dismissToast,
  }
}
