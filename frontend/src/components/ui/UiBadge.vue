<script setup lang="ts">
import { computed, type Component } from 'vue'
import type { CategoryColor } from '../../appearance/entities'

const props = withDefaults(defineProps<{
  tone?: 'neutral' | 'accent' | 'accent-2' | 'success' | 'warning' | 'danger' | 'info'
  category?: CategoryColor
  icon?: Component
  /** A dot before the text; the text still names the state, so colour is never the only signal (ТЗ 10). */
  dot?: boolean
}>(), { tone: 'neutral' })
const style = computed(() => (props.category ? { '--badge-color': `var(--${props.category})` } : undefined))
</script>

<template>
  <span class="ui-badge" :class="[category ? 'category' : tone]" :style="style">
    <span v-if="dot" class="dot" aria-hidden="true"></span>
    <component :is="icon" v-if="icon" class="icon" aria-hidden="true" />
    <slot />
  </span>
</template>

<style scoped>
.ui-badge {
  --badge-color: var(--text-muted);
  display: inline-flex; align-items: center; gap: .3rem; min-height: 22px; padding: 0 .5rem;
  border: 1px solid color-mix(in oklab, var(--badge-color) 40%, transparent); border-radius: var(--radius-full);
  color: var(--badge-ink, var(--text)); background-color: color-mix(in oklab, var(--badge-color) 14%, var(--surface-1));
  font-weight: 650; font-size: var(--font-size-2xs); line-height: 1; white-space: nowrap;
}
.accent { --badge-color: var(--accent); }
.accent-2 { --badge-color: var(--accent-2); }
.success { --badge-color: var(--success); --badge-ink: var(--status-success-text); }
.warning { --badge-color: var(--warning); --badge-ink: var(--status-warning-text); }
.danger { --badge-color: var(--danger); --badge-ink: var(--status-danger-text); }
.info { --badge-color: var(--info); --badge-ink: var(--status-info-text); }
.dot { width: 6px; height: 6px; border-radius: 50%; background-color: var(--badge-color); }
.icon { width: 12px; height: 12px; }
</style>
