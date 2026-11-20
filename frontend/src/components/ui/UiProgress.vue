<script setup lang="ts">
import { computed } from 'vue'

// Linear or ring progress. `meter` colours the fill success → warning → error by how much is used (ТЗ 7.1).
const props = withDefaults(defineProps<{
  value: number
  max?: number
  label: string
  variant?: 'linear' | 'ring'
  /** Shows the share left as a gradient bar instead of an accent fill. */
  meter?: boolean
  size?: number
  showValue?: boolean
  indeterminate?: boolean
}>(), { max: 100, variant: 'linear', size: 28 })

const ratio = computed(() => (props.max > 0 ? Math.min(1, Math.max(0, props.value / props.max)) : 0))
const percent = computed(() => Math.round(ratio.value * 100))
const tone = computed(() => (!props.meter ? 'accent' : ratio.value <= .15 ? 'danger' : ratio.value <= .4 ? 'warning' : 'success'))
const radius = 15.9155
</script>

<template>
  <span class="ui-progress" :class="[variant, tone, { indeterminate }]" role="progressbar" :aria-label="label"
    :aria-valuemin="indeterminate ? undefined : 0" :aria-valuemax="indeterminate ? undefined : max"
    :aria-valuenow="indeterminate ? undefined : value">
    <template v-if="variant === 'linear'">
      <span class="track"><span class="fill" :style="{ transform: `scaleX(${indeterminate ? .35 : ratio})` }"></span></span>
    </template>
    <svg v-else :width="size" :height="size" viewBox="0 0 36 36" aria-hidden="true">
      <circle class="ring-track" cx="18" cy="18" :r="radius" />
      <circle class="ring-fill" cx="18" cy="18" :r="radius" :stroke-dasharray="`${percent} 100`" />
    </svg>
    <span v-if="showValue" class="value">{{ percent }}%</span>
  </span>
</template>

<style scoped>
.ui-progress { --progress-color: var(--accent); display: inline-flex; align-items: center; gap: var(--space-2); width: 100%; }
.ring { width: auto; }
.success { --progress-color: var(--success); }
.warning { --progress-color: var(--warning); }
.danger { --progress-color: var(--danger); }
.track { position: relative; flex: 1; height: 6px; overflow: hidden; border-radius: var(--radius-full); background-color: var(--surface-2); }
.fill {
  position: absolute; inset: 0; border-radius: inherit; background-color: var(--progress-color); transform-origin: left;
  transition: transform var(--motion-slow) var(--ease-out), background-color var(--motion-base);
}
/* The balance bar is one of the places where a gradient is allowed (ТЗ 4.6). */
.ui-progress:is(.success, .warning, .danger) .fill {
  background-image: linear-gradient(90deg, color-mix(in oklab, var(--progress-color) 55%, var(--surface-1)), var(--progress-color));
}
.indeterminate .fill { animation: ui-slide 1.2s var(--ease-out) infinite; }
@keyframes ui-slide { from { transform: translateX(-40%) scaleX(.35); } to { transform: translateX(110%) scaleX(.35); } }
svg { flex: 0 0 auto; transform: rotate(-90deg); }
circle { fill: none; stroke-width: 4; }
.ring-track { stroke: var(--surface-2); }
.ring-fill { stroke: var(--progress-color); stroke-linecap: round; transition: stroke-dasharray var(--motion-slow) var(--ease-out), stroke var(--motion-base); }
.value { min-width: 2.6em; color: var(--text-muted); font-weight: 650; font-size: var(--font-size-xs); font-variant-numeric: tabular-nums; }
</style>
