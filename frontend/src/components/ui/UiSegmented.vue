<script setup lang="ts" generic="T extends string | number">
import { computed, nextTick, onMounted, ref, useId, watch, type Component } from 'vue'

// A radio group drawn as segments; arrow keys move the choice, and a sliding thumb marks it (ТЗ 5).
const model = defineModel<T>({ required: true })
const props = withDefaults(defineProps<{
  label: string
  options: readonly { value: T; label: string; icon?: Component }[]
  hideLabel?: boolean
  size?: 'sm' | 'md'
  block?: boolean
}>(), { size: 'md' })

const name = `segmented-${useId()}`
const root = ref<HTMLElement>()
const thumb = ref({ left: 0, width: 0, ready: false })
const index = computed(() => props.options.findIndex((option) => option.value === model.value))

function measure() {
  const element = root.value?.querySelectorAll<HTMLElement>('.segment')[index.value]
  if (!element) { thumb.value = { left: 0, width: 0, ready: false }; return }
  thumb.value = { left: element.offsetLeft, width: element.offsetWidth, ready: true }
}
watch([index, () => props.options], () => void nextTick(measure))
onMounted(() => {
  measure()
  if (root.value && 'ResizeObserver' in window) new ResizeObserver(measure).observe(root.value)
})
</script>

<template>
  <fieldset class="ui-segmented" :class="[size, { block }]">
    <legend :class="{ 'sr-only': hideLabel }">{{ label }}</legend>
    <div ref="root" class="track">
      <span v-if="thumb.ready" class="thumb" aria-hidden="true"
        :style="{ '--segment-width': `${thumb.width}px`, transform: `translateX(${thumb.left}px)` }"></span>
      <label v-for="option in options" :key="option.value" class="segment" :class="{ active: option.value === model }">
        <input v-model="model" type="radio" :name="name" :value="option.value" />
        <component :is="option.icon" v-if="option.icon" class="icon" aria-hidden="true" />
        <span>{{ option.label }}</span>
      </label>
    </div>
  </fieldset>
</template>

<style scoped>
.ui-segmented { min-width: 0; margin: 0; padding: 0; border: 0; }
legend { margin-bottom: var(--space-2); padding: 0; font-weight: 600; font-size: var(--font-size-sm); }
.track {
  position: relative; display: inline-flex; max-width: 100%; padding: 3px; overflow-x: auto; border: 1px solid var(--border);
  border-radius: var(--radius-md); background-color: var(--surface-2); scrollbar-width: none;
}
.block .track { display: flex; width: 100%; }
.block .segment { flex: 1 1 0; }
.thumb {
  position: absolute; top: 3px; bottom: 3px; left: 0; width: var(--segment-width); border-radius: max(0px, calc(var(--radius-md) - 3px));
  background-color: var(--surface-1); box-shadow: var(--elevation-1), 0 0 0 1px var(--border);
  transition: transform var(--motion-base) var(--ease-out);
}
.segment {
  position: relative; display: inline-flex !important; grid-auto-flow: column; align-items: center; justify-content: center;
  gap: .4rem; min-height: 34px; padding: 0 var(--space-3); border-radius: calc(var(--radius-md) - 3px);
  color: var(--text-muted); font-weight: 600; font-size: var(--font-size-sm); white-space: nowrap; cursor: pointer;
  transition: color var(--motion-base) var(--ease-out);
}
.sm .segment { min-height: 28px; padding-inline: var(--space-2); font-size: var(--font-size-xs); }
.segment:hover { color: var(--text); }
.segment.active { color: var(--text); font-weight: 700; }
.segment:active { transform: scale(.97); }
.segment input { position: absolute; inset: 0; z-index: 1; width: 100%; height: 100%; margin: 0; opacity: 0; cursor: pointer; }
.segment:has(input:focus-visible) { outline: 3px solid var(--focus); outline-offset: 1px; }
.icon { width: 16px; height: 16px; }
</style>
