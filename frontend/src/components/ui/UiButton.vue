<script setup lang="ts">
import type { Component } from 'vue'
import { RouterLink } from 'vue-router'

withDefaults(defineProps<{
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  type?: 'button' | 'submit' | 'reset'
  icon?: Component
  iconRight?: Component
  loading?: boolean
  disabled?: boolean
  block?: boolean
  /** Renders a router link styled as a button. */
  to?: string
}>(), { variant: 'secondary', size: 'md', type: 'button' })
</script>

<template>
  <component :is="to ? RouterLink : 'button'" class="ui-button" :class="[variant, size, { block, loading }]"
    v-bind="to ? { to } : { type, disabled: disabled || loading }" :aria-busy="loading || undefined">
    <span class="content">
      <component :is="icon" v-if="icon" class="icon" aria-hidden="true" />
      <slot />
      <component :is="iconRight" v-if="iconRight" class="icon trailing" aria-hidden="true" />
    </span>
    <span v-if="loading" class="spinner" aria-hidden="true"></span>
  </component>
</template>

<style scoped>
.ui-button {
  --button-bg: transparent; --button-ink: var(--text); --button-border: var(--dark-rule);
  position: relative; isolation: isolate; display: inline-flex; align-items: center; justify-content: center;
  min-height: 40px; padding: 0 var(--space-4); overflow: hidden; border: 1px solid var(--button-border);
  border-radius: var(--radius-md); color: var(--button-ink); background-color: var(--button-bg);
  font-weight: 650; font-size: var(--font-size-sm); line-height: 1.2; text-decoration: none; white-space: nowrap;
  transition: transform var(--motion-fast) var(--ease-out), box-shadow var(--motion-base) var(--ease-out),
    background-color var(--motion-base) var(--ease-out), border-color var(--motion-base) var(--ease-out), color var(--motion-base) var(--ease-out);
}
.ui-button.sm { min-height: 34px; padding-inline: var(--space-3); font-size: var(--font-size-xs); }
.ui-button.lg { min-height: 46px; padding-inline: var(--space-5); font-size: var(--font-size-md); }
.block { width: 100%; }
.content { display: inline-flex; align-items: center; gap: .45em; transition: opacity var(--motion-fast); }
.icon { width: 1.15em; height: 1.15em; flex: 0 0 auto; }
.trailing { transition: transform var(--motion-base) var(--ease-out); }

.primary { --button-bg: var(--btn-primary-bg); --button-ink: var(--btn-primary-text); --button-border: var(--btn-primary-bg); }
/* The gradient sits in its own layer so it can fade: background-image itself does not animate (ТЗ 4.6). */
.primary::before {
  content: ""; position: absolute; inset: 0; z-index: -1; opacity: 0;
  background: linear-gradient(135deg, var(--btn-primary-bg), color-mix(in oklab, var(--btn-primary-bg), var(--accent-2) 40%));
  transition: opacity var(--motion-base) var(--ease-out);
}
.ghost { --button-border: transparent; --button-ink: var(--accent); }
.danger { --button-bg: var(--danger-fill); --button-ink: var(--on-fill); --button-border: var(--danger-fill); }

@media (hover: hover) {
  .ui-button:not(:disabled):hover { transform: translateY(-1px); }
  .primary:not(:disabled):hover { box-shadow: var(--glow-md); }
  .primary:not(:disabled):hover::before { opacity: 1; }
  .secondary:not(:disabled):hover, .ghost:not(:disabled):hover {
    --button-bg: color-mix(in oklab, var(--accent) 8%, transparent); --button-border: var(--accent);
  }
  .danger:not(:disabled):hover { box-shadow: 0 6px 18px -6px var(--danger-fill); }
  .ui-button:not(:disabled):hover .trailing { transform: translateX(2px); }
}
.ui-button:not(:disabled):active { transform: scale(.97); }
.primary:not(:disabled):active::before { opacity: 1; }
.ui-button:focus-visible { box-shadow: var(--glow-sm); }
.ui-button:disabled { opacity: .5; cursor: not-allowed; }
.loading, .loading:disabled { opacity: 1; cursor: progress; }
.loading .content { opacity: 0; }
.spinner {
  position: absolute; inset: 0; width: 1.1em; height: 1.1em; margin: auto; border: 2px solid currentColor;
  border-right-color: transparent; border-radius: 50%; animation: ui-spin .7s linear infinite;
}
@keyframes ui-spin { to { transform: rotate(1turn); } }
</style>
