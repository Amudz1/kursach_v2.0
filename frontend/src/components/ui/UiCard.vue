<script setup lang="ts">
import { computed } from 'vue'
import type { CategoryColor } from '../../appearance/entities'

// A surface; `interactive` lifts it on hover, `category` colours the edge, `spotlight` follows the pointer (ТЗ 4.6, 5).
const props = withDefaults(defineProps<{
  as?: string
  category?: CategoryColor
  interactive?: boolean
  spotlight?: boolean
  selected?: boolean
  padding?: 'none' | 'sm' | 'md' | 'lg'
  loading?: boolean
}>(), { as: 'section', padding: 'md' })

const style = computed(() => (props.category ? { '--card-category': `var(--${props.category})` } : undefined))

function track(event: PointerEvent) {
  if (!props.spotlight) return
  const target = event.currentTarget as HTMLElement
  const box = target.getBoundingClientRect()
  target.style.setProperty('--mx', `${event.clientX - box.left}px`)
  target.style.setProperty('--my', `${event.clientY - box.top}px`)
}
</script>

<template>
  <component :is="as" class="ui-card" :class="[`pad-${padding}`, { interactive, spotlight, selected, categorized: category }]"
    :style="style" :aria-busy="loading || undefined" @pointermove="track">
    <template v-if="loading">
      <span class="skeleton-line wide"></span>
      <span class="skeleton-line"></span>
      <span class="skeleton-line short"></span>
    </template>
    <slot v-else />
  </component>
</template>

<style scoped>
.ui-card {
  position: relative; min-width: 0; border: 1px solid var(--border); border-radius: var(--radius-lg);
  background-color: var(--surface-1); box-shadow: var(--elevation-0);
  transition: transform var(--motion-base) var(--ease-out), box-shadow var(--motion-base) var(--ease-out), border-color var(--motion-base) var(--ease-out);
}
.pad-sm { padding: var(--space-3); }
.pad-md { padding: var(--space-5); }
.pad-lg { padding: var(--space-7); }
.categorized { border-left: 3px solid var(--card-category); }
.selected { border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent); }
.interactive { cursor: pointer; }
@media (hover: hover) {
  .interactive:hover { transform: translateY(-2px); box-shadow: var(--elevation-2); border-color: var(--dark-rule); }
  .interactive.categorized:hover { border-left-color: var(--card-category); }
  .interactive.selected:hover { border-color: var(--accent); }
}
.interactive:active { transform: translateY(0) scale(.99); }
.interactive:focus-visible, .interactive:focus-within:has(:focus-visible) { box-shadow: var(--glow-sm); }

/* Spotlight only with bright glow, a precise pointer and motion on (ТЗ 4.6). */
.spotlight::after {
  content: ""; position: absolute; inset: 0; border-radius: inherit; pointer-events: none; opacity: 0;
  background: radial-gradient(260px circle at var(--mx, 50%) var(--my, 50%), color-mix(in oklab, var(--accent) 16%, transparent), transparent 70%);
  transition: opacity var(--motion-base) var(--ease-out);
}
:root:not([data-glow="bright"]) .spotlight::after,
:root:is([data-motion="reduced"], [data-motion="off"]) .spotlight::after { display: none; }
@media (hover: hover) { .spotlight:hover::after { opacity: 1; } }
@media (hover: none), (prefers-reduced-motion: reduce) { .spotlight::after { display: none; } }

.skeleton-line {
  display: block; height: .8rem; width: 70%; margin-block: .55rem; border-radius: var(--radius-xs);
  background-color: var(--surface-2); animation: ui-pulse 1.4s ease-in-out infinite;
}
.skeleton-line.wide { width: 45%; height: 1.05rem; }
.skeleton-line.short { width: 40%; }
@keyframes ui-pulse { 50% { opacity: .45; } }
</style>
