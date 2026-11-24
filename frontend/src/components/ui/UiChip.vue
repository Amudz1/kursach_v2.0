<script setup lang="ts">
import { computed, type Component } from 'vue'
import { ArrowRight } from 'lucide-vue-next'
import type { CategoryColor } from '../../appearance/entities'

// A suggestion or filter: a category fill of 15 %, an arrow that slides in on hover, press at .96 (ТЗ 5).
const props = withDefaults(defineProps<{
  category?: CategoryColor
  icon?: Component
  pressed?: boolean
  arrow?: boolean
  disabled?: boolean
  removable?: boolean
}>(), { pressed: undefined })
defineEmits<{ remove: [] }>()
const style = computed(() => (props.category ? { '--chip-color': `var(--${props.category})` } : undefined))
</script>

<template>
  <button type="button" class="ui-chip" :class="{ arrow, categorized: category }" :style="style" :aria-pressed="pressed" :disabled="disabled">
    <component :is="icon" v-if="icon" class="icon" aria-hidden="true" />
    <span class="label"><slot /></span>
    <ArrowRight v-if="arrow" class="arrow-icon" aria-hidden="true" />
  </button>
</template>

<style scoped>
.ui-chip {
  --chip-color: var(--accent);
  display: inline-flex; align-items: center; gap: .4rem; min-height: 32px; padding: 0 .75rem;
  border: 1px solid color-mix(in oklab, var(--chip-color) 35%, var(--border)); border-radius: var(--radius-full);
  color: var(--text); background-color: color-mix(in oklab, var(--chip-color) 15%, var(--surface-1));
  font-weight: 600; font-size: var(--font-size-xs); text-align: left;
  transition: transform var(--motion-fast) var(--ease-out), background-color var(--motion-base) var(--ease-out),
    border-color var(--motion-base) var(--ease-out), box-shadow var(--motion-base) var(--ease-out);
}
.icon { width: 15px; height: 15px; flex: 0 0 auto; color: var(--chip-color); }
:root[data-theme="dark"] .icon { color: color-mix(in oklab, var(--chip-color), white 45%); }
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) .icon { color: color-mix(in oklab, var(--chip-color), white 45%); } }
.arrow-icon { width: 14px; height: 14px; margin-right: -.2rem; opacity: 0; transform: translateX(-4px);
  transition: opacity var(--motion-base) var(--ease-out), transform var(--motion-base) var(--ease-out); }
@media (hover: hover) {
  .ui-chip:not(:disabled):hover { background-color: color-mix(in oklab, var(--chip-color) 24%, var(--surface-1)); border-color: var(--chip-color); }
  .ui-chip:not(:disabled):hover .arrow-icon { opacity: 1; transform: translateX(0); }
}
.ui-chip:focus-visible .arrow-icon { opacity: 1; transform: translateX(0); }
.ui-chip:not(:disabled):active { transform: scale(.96); }
.ui-chip[aria-pressed=true] { border-color: var(--chip-color); box-shadow: inset 0 0 0 1px var(--chip-color);
  background-color: color-mix(in oklab, var(--chip-color) 28%, var(--surface-1)); font-weight: 700; }
.ui-chip:disabled { opacity: .5; cursor: not-allowed; }
</style>
