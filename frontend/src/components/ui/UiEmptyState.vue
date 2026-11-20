<script setup lang="ts">
import type { Component } from 'vue'

defineProps<{ title: string; text?: string; icon?: Component }>()
</script>

<template>
  <div class="ui-empty">
    <span v-if="icon" class="badge" aria-hidden="true"><component :is="icon" /></span>
    <slot name="illustration" />
    <h3>{{ title }}</h3>
    <p v-if="text">{{ text }}</p>
    <div v-if="$slots.default" class="actions"><slot /></div>
  </div>
</template>

<style scoped>
.ui-empty { display: grid; justify-items: center; gap: var(--space-3); padding: var(--space-7) var(--space-4); text-align: center; }
.badge {
  display: grid; place-items: center; width: 52px; height: 52px; border-radius: var(--radius-full);
  color: var(--accent); background-color: color-mix(in oklab, var(--accent) 12%, var(--surface-1));
  box-shadow: inset 0 0 0 1px color-mix(in oklab, var(--accent) 25%, transparent);
}
.badge svg { width: 24px; height: 24px; }
p { max-width: 34rem; color: var(--text-muted); font-size: var(--font-size-sm); }
.actions { display: flex; flex-wrap: wrap; justify-content: center; gap: var(--space-2); margin-top: var(--space-2); }
</style>
