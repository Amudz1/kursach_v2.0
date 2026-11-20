<script setup lang="ts">
import { onBeforeUnmount, ref, useId } from 'vue'

// Shows after 400 ms on hover and at once on keyboard focus; Esc hides it (ТЗ 5, 10).
const props = withDefaults(defineProps<{ text: string; placement?: 'top' | 'bottom'; delay?: number; disabled?: boolean }>(),
  { placement: 'top', delay: 400 })
const id = `tip-${useId()}`
const open = ref(false)
let timer: number | undefined

function show(immediate = false) {
  if (props.disabled) return
  window.clearTimeout(timer)
  if (immediate) open.value = true
  else timer = window.setTimeout(() => { open.value = true }, props.delay)
}
function hide() {
  window.clearTimeout(timer)
  open.value = false
}
function onFocus(event: FocusEvent) {
  const target = event.target as HTMLElement | null
  if (target?.matches(':focus-visible')) show(true)
}
onBeforeUnmount(() => window.clearTimeout(timer))
</script>

<template>
  <span class="tooltip-anchor" @pointerenter="show()" @pointerleave="hide" @focusin="onFocus" @focusout="hide"
    @keydown.esc="hide" @pointerdown="hide">
    <slot :describedby="id" />
    <span v-if="!disabled" :id="id" role="tooltip" class="tooltip" :class="[placement, { open }]">{{ text }}</span>
  </span>
</template>

<style scoped>
.tooltip-anchor { position: relative; display: inline-flex; }
.tooltip {
  position: absolute; z-index: 30; left: 50%; width: max-content; max-width: 16rem; padding: .3rem .55rem;
  border-radius: var(--radius-sm); color: var(--surface-1); background: var(--text); box-shadow: var(--elevation-2);
  font-size: var(--font-size-2xs); font-weight: 600; line-height: 1.3; pointer-events: none; white-space: normal;
  opacity: 0; transform: translate(-50%, 4px); visibility: hidden;
  transition: opacity var(--motion-fast) var(--ease-out), transform var(--motion-fast) var(--ease-out), visibility var(--motion-fast);
}
.tooltip.top { bottom: calc(100% + 6px); }
.tooltip.bottom { top: calc(100% + 6px); transform: translate(-50%, -4px); }
.tooltip.open { opacity: 1; transform: translate(-50%, 0); visibility: visible; }
</style>
