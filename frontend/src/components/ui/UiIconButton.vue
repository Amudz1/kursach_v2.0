<script setup lang="ts">
import type { Component } from 'vue'
import UiTooltip from './UiTooltip.vue'

// Every icon button has an accessible name and a tooltip with the same text (ТЗ 5, 10).
withDefaults(defineProps<{
  icon: Component
  label: string
  variant?: 'ghost' | 'solid' | 'primary' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  pressed?: boolean
  disabled?: boolean
  type?: 'button' | 'submit'
  tooltip?: boolean
  placement?: 'top' | 'bottom'
}>(), { variant: 'ghost', size: 'md', type: 'button', tooltip: true, placement: 'top', pressed: undefined })
</script>

<template>
  <UiTooltip :text="label" :placement="placement" :disabled="!tooltip">
    <button :type="type" class="ui-icon-button" :class="[variant, size, { pressed }]" :aria-label="label"
      :aria-pressed="pressed" :disabled="disabled">
      <slot><component :is="icon" class="icon" aria-hidden="true" /></slot>
    </button>
  </UiTooltip>
</template>

<style scoped>
.ui-icon-button {
  display: inline-grid; place-items: center; width: 36px; height: 36px; padding: 0; border: 1px solid transparent;
  border-radius: var(--radius-full); color: var(--text-muted); background-color: transparent;
  transition: transform var(--motion-fast) var(--ease-out), background-color var(--motion-base) var(--ease-out),
    color var(--motion-base) var(--ease-out), box-shadow var(--motion-base) var(--ease-out), border-color var(--motion-base);
}
.sm { width: 30px; height: 30px; }
.lg { width: 44px; height: 44px; }
.icon { width: 18px; height: 18px; }
.sm .icon { width: 16px; height: 16px; }
.lg .icon { width: 20px; height: 20px; }
.solid { color: var(--text); background-color: var(--surface-2); border-color: var(--border); }
.primary { color: var(--btn-primary-text); background-color: var(--btn-primary-bg); }
.danger { color: var(--danger); }
.pressed { color: var(--accent); background-color: color-mix(in oklab, var(--accent) 14%, transparent); border-color: var(--accent); }
@media (hover: hover) {
  .ui-icon-button:not(:disabled):hover { color: var(--text); background-color: var(--surface-2); }
  .primary:not(:disabled):hover { color: var(--btn-primary-text); background-color: var(--btn-primary-bg); box-shadow: var(--glow-md); }
  .danger:not(:disabled):hover { color: var(--danger); background-color: var(--status-danger-bg); }
}
.ui-icon-button:not(:disabled):active { transform: scale(.92); }
.ui-icon-button:focus-visible { box-shadow: var(--glow-sm); }
.ui-icon-button:disabled { opacity: .5; cursor: not-allowed; }
@media (max-width: 900px) and (pointer: coarse) {
  .ui-icon-button, .sm { width: 40px; height: 40px; }
}
</style>
