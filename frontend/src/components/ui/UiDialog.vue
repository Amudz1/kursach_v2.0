<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { X } from 'lucide-vue-next'
import { focusableIn, lockScroll, trapTab, unlockScroll } from './focus'

// A modal dialog: focus moves in and is trapped, Esc and the backdrop close it, focus returns on close (ТЗ 10).
const open = defineModel<boolean>({ default: false })
const props = withDefaults(defineProps<{ title: string; description?: string; size?: 'sm' | 'md' | 'lg'; dismissible?: boolean }>(),
  { size: 'md', dismissible: true })
const panel = ref<HTMLElement>()
const id = `dialog-${useId()}`
let returnTo: HTMLElement | null = null
let locked = false

watch(open, async (value) => {
  if (value) {
    returnTo = document.activeElement as HTMLElement | null
    lockScroll()
    locked = true
    await nextTick()
    if (!open.value || !panel.value) return
    const target = panel.value.querySelector<HTMLElement>('[autofocus]') ?? focusableIn(panel.value)[0] ?? panel.value
    target.focus()
  } else if (locked) {
    locked = false
    unlockScroll()
    if (returnTo?.isConnected) returnTo.focus({ preventScroll: true })
  }
}, { immediate: true })
onBeforeUnmount(() => { if (locked) unlockScroll() })

function close() { open.value = false }
function onKey(event: KeyboardEvent) {
  if (event.key === 'Escape' && props.dismissible) { event.preventDefault(); close() }
  else if (panel.value) trapTab(event, panel.value)
}
</script>

<template>
  <Teleport to="body">
    <Transition name="ui-dialog">
      <div v-if="open" class="ui-dialog-backdrop" @click.self="dismissible && close()">
        <div :id="id" ref="panel" class="panel" :class="size" role="dialog" aria-modal="true" :aria-labelledby="`${id}-title`"
          :aria-describedby="description ? `${id}-description` : undefined" tabindex="-1" @keydown="onKey">
          <header>
            <h2 :id="`${id}-title`">{{ title }}</h2>
            <button v-if="dismissible" type="button" class="close" aria-label="Закрыть" @click="close"><X aria-hidden="true" /></button>
          </header>
          <p v-if="description" :id="`${id}-description`" class="description">{{ description }}</p>
          <div class="body"><slot :close="close" /></div>
          <footer v-if="$slots.footer"><slot name="footer" :close="close" /></footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.ui-dialog-backdrop {
  position: fixed; inset: 0; z-index: 60; display: grid; place-items: center; padding: var(--space-4);
  background-color: color-mix(in oklab, var(--c-ink-900) 45%, transparent);
}
.panel {
  display: grid; gap: var(--space-4); width: min(100%, 32rem); max-height: calc(100dvh - 2rem); overflow-y: auto;
  padding: var(--space-6); border: 1px solid var(--border); border-radius: var(--radius-xl);
  background-color: var(--surface-1); box-shadow: var(--elevation-3);
}
.panel.sm { width: min(100%, 24rem); }
.panel.lg { width: min(100%, 46rem); }
.panel:focus-visible { outline: none; }
header { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--space-3); }
h2 { font-size: var(--font-size-xl); letter-spacing: -.02em; }
.description { color: var(--text-muted); font-size: var(--font-size-sm); }
.close {
  display: grid; place-items: center; flex: 0 0 auto; width: 36px; height: 36px; margin: -.35rem -.35rem 0 0; padding: 0;
  border: 0; border-radius: var(--radius-full); color: var(--text-muted); background-color: transparent;
  transition: background-color var(--motion-fast), color var(--motion-fast), transform var(--motion-fast);
}
.close:hover { color: var(--text); background-color: var(--surface-2); }
.close:active { transform: scale(.92); }
.close svg { width: 18px; height: 18px; }
footer { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: var(--space-2); }
@media (max-width: 900px) {
  .close, footer :deep(button) { min-width: 44px; min-height: 44px; }
  footer :deep(button) { max-width: 100%; white-space: normal; }
}
.ui-dialog-enter-active, .ui-dialog-leave-active { transition: opacity var(--motion-base) var(--ease-out); }
.ui-dialog-enter-active .panel, .ui-dialog-leave-active .panel { transition: transform var(--motion-base) var(--ease-out), opacity var(--motion-base); }
.ui-dialog-enter-from, .ui-dialog-leave-to { opacity: 0; }
.ui-dialog-enter-from .panel, .ui-dialog-leave-to .panel { opacity: 0; transform: translateY(8px) scale(.98); }
</style>
