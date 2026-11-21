<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { lockScroll, trapTab, unlockScroll } from './ui/focus'

// Mobile navigation panel (ТЗ 7.1): traps focus, locks page scroll, closes on Esc, backdrop or a swipe to the left.
const props = defineProps<{ id: string; label: string; returnFocus?: HTMLElement | null }>()
const open = defineModel<boolean>('open', { required: true })
const panel = ref<HTMLElement>()
let startX: number | null = null

function close() { open.value = false }
function onKey(event: KeyboardEvent) {
  if (event.key === 'Escape') { event.preventDefault(); close() }
  else if (panel.value) trapTab(event, panel.value)
}
function onTouchStart(event: TouchEvent) { startX = event.touches[0]?.clientX ?? null }
function onTouchEnd(event: TouchEvent) {
  const endX = event.changedTouches[0]?.clientX
  if (startX !== null && endX !== undefined && startX - endX > 60) close()
  startX = null
}

watch(open, async (value) => {
  if (value) {
    lockScroll()
    await nextTick()
    const target = panel.value?.querySelector<HTMLElement>('[aria-current]') ?? panel.value?.querySelector<HTMLElement>('a, button')
    target?.focus()
  } else {
    unlockScroll()
    props.returnFocus?.focus()
  }
})
onBeforeUnmount(() => { if (open.value) unlockScroll() })
</script>

<template>
  <Teleport to="body">
    <Transition name="drawer">
      <div v-if="open" class="drawer-layer" @keydown="onKey">
        <div class="drawer-backdrop" aria-hidden="true" @click="close"></div>
        <div :id="id" ref="panel" class="drawer" role="dialog" aria-modal="true" :aria-label="label" tabindex="-1"
          @touchstart.passive="onTouchStart" @touchend="onTouchEnd">
          <slot :close="close" />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.drawer-layer { position: fixed; inset: 0; z-index: 50; }
.drawer-backdrop { position: absolute; inset: 0; background-color: color-mix(in srgb, var(--c-ink-900) 45%, transparent); }
.drawer {
  position: absolute; inset: 0 auto 0 0; display: flex; flex-direction: column; gap: var(--space-1);
  width: min(84vw, 300px); padding: var(--space-3); overflow-y: auto; overscroll-behavior: contain;
  background-color: var(--surface-1); border-right: 1px solid var(--border); box-shadow: var(--elevation-3);
}
.drawer:focus { outline: none; }
.drawer-enter-active, .drawer-leave-active { transition: opacity var(--motion-base) var(--ease-out); }
.drawer-enter-active .drawer, .drawer-leave-active .drawer { transition: transform var(--motion-base) var(--ease-out); }
.drawer-enter-from, .drawer-leave-to { opacity: 0; }
.drawer-enter-from .drawer, .drawer-leave-to .drawer { transform: translateX(-100%); }
</style>
