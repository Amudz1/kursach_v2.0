<script setup lang="ts">
import { computed, type Component } from 'vue'
import { categoryFor, type CategoryColor } from '../../appearance/entities'

// A round avatar. Without a colour, one is derived from `seed` so the same user always looks the same (ТЗ 7.1).
const props = withDefaults(defineProps<{
  name: string
  seed?: string
  color?: CategoryColor
  icon?: Component
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
  /** The assistant avatar uses the accent gradient (ТЗ 4.6). */
  brand?: boolean
  decorative?: boolean
}>(), { size: 'md' })

const category = computed(() => props.color ?? categoryFor(props.seed ?? props.name))
const initial = computed(() => props.name.trim().slice(0, 1).toUpperCase() || '?')
</script>

<template>
  <span class="ui-avatar" :class="[size, { brand }]" :style="{ '--avatar-color': `var(--${category})` }"
    :role="decorative ? undefined : 'img'" :aria-label="decorative ? undefined : name" :aria-hidden="decorative || undefined">
    <component :is="icon" v-if="icon" class="icon" aria-hidden="true" />
    <span v-else aria-hidden="true">{{ initial }}</span>
  </span>
</template>

<style scoped>
.ui-avatar {
  display: inline-grid; place-items: center; flex: 0 0 auto; width: 32px; height: 32px; border-radius: var(--radius-full);
  color: var(--avatar-category-text, var(--on-fill)); background-color: var(--avatar-color); font-weight: 750; font-size: .85rem; line-height: 1;
  box-shadow: inset 0 0 0 1px color-mix(in oklab, white 18%, transparent); user-select: none;
}
.xs { width: 20px; height: 20px; font-size: .62rem; }
.sm { width: 26px; height: 26px; font-size: .72rem; }
.lg { width: 44px; height: 44px; font-size: 1.1rem; }
.xl { width: 72px; height: 72px; font-size: 1.8rem; }
.brand { background-color: var(--btn-primary-bg); background-image: linear-gradient(140deg, var(--btn-primary-bg),
  color-mix(in oklab, var(--btn-primary-bg), var(--accent-2) 18%)); color: var(--on-accent); }
:root[data-avatar="custom"] .brand { background-image: none; background-color: var(--avatar-custom, var(--btn-primary-bg));
  color: var(--avatar-custom-text, var(--btn-primary-text)); }
.icon { width: 58%; height: 58%; }
</style>
