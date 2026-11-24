// Focus trap and scroll lock shared by the dialog and the mobile drawer (ТЗ 7.1, 10).

const focusable = 'a[href], button:not([disabled]), input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])'

export function focusableIn(container: HTMLElement) {
  return [...container.querySelectorAll<HTMLElement>(focusable)]
    .filter(element => element.getClientRects().length > 0 && getComputedStyle(element).visibility === 'visible')
}

/** Keeps Tab and Shift+Tab inside `container`. Call from a keydown handler. */
export function trapTab(event: KeyboardEvent, container: HTMLElement) {
  if (event.key !== 'Tab') return
  const list = focusableIn(container)
  if (!list.length) { event.preventDefault(); container.focus(); return }
  const first = list[0], last = list[list.length - 1]
  if (event.shiftKey && (document.activeElement === first || document.activeElement === container)) { event.preventDefault(); last.focus() }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
}

let locks = 0
let previousOverflow = ''

export function lockScroll() {
  if (locks++ === 0) {
    previousOverflow = document.documentElement.style.overflow
    document.documentElement.style.overflow = 'hidden'
  }
}

export function unlockScroll() {
  if (locks === 0) return
  if (--locks === 0) document.documentElement.style.overflow = previousOverflow
}
