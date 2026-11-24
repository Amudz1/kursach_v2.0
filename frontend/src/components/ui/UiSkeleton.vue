<script setup lang="ts">
withDefaults(defineProps<{ lines?: number; width?: string; height?: string; circle?: boolean }>(), { lines: 1 })
</script>

<template>
  <span class="ui-skeleton" aria-hidden="true">
    <span v-for="line in lines" :key="line" class="bar" :class="{ circle }"
      :style="{ width: circle ? height : line === lines && lines > 1 ? '60%' : width, height }"></span>
  </span>
</template>

<style scoped>
.ui-skeleton { display: grid; gap: var(--space-2); }
.bar {
  display: block; width: 100%; height: .85rem; border-radius: var(--radius-xs); background-color: var(--surface-2);
  animation: ui-pulse 1.4s ease-in-out infinite;
}
.circle { border-radius: 50%; aspect-ratio: 1; }
@keyframes ui-pulse { 50% { opacity: .45; } }
</style>
