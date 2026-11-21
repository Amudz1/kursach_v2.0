<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch, type Component } from 'vue'

// WAI-ARIA tabs: arrows, Home and End move focus and selection; the underline slides to the active tab (ТЗ 5, 7.7).
const model = defineModel<string>({ required: true })
const props = defineProps<{ label: string; tabs: readonly { value: string; label: string; icon?: Component }[] }>()
const base = `tabs-${useId()}`
const list = ref<HTMLElement>()
const indicator = ref({ left: 0, width: 0, ready: false })
const index = computed(() => props.tabs.findIndex((tab) => tab.value === model.value))
let resize: ResizeObserver | undefined

function measure() {
  const element = list.value?.querySelectorAll<HTMLElement>('[role=tab]')[index.value]
  indicator.value = element ? { left: element.offsetLeft, width: element.offsetWidth, ready: true } : { left: 0, width: 0, ready: false }
}
function select(next: number) {
  const count = props.tabs.length
  if (!count) return
  const target = props.tabs[(next + count) % count]
  model.value = target.value
  void nextTick(() => list.value?.querySelectorAll<HTMLElement>('[role=tab]')[(next + count) % count]?.focus())
}
function onKey(event: KeyboardEvent) {
  const moves: Record<string, number> = { ArrowRight: index.value + 1, ArrowLeft: index.value - 1, Home: 0, End: props.tabs.length - 1 }
  if (!(event.key in moves)) return
  event.preventDefault()
  select(moves[event.key])
}
watch([index, () => props.tabs], () => void nextTick(measure))
onMounted(() => {
  measure()
  if (list.value && 'ResizeObserver' in window) {
    resize = new ResizeObserver(measure)
    resize.observe(list.value)
    for (const tab of list.value.querySelectorAll('[role=tab]')) resize.observe(tab)
  }
})
onBeforeUnmount(() => resize?.disconnect())
const tabId = (value: string) => `${base}-tab-${value}`
const panelId = (value: string) => `${base}-panel-${value}`
defineExpose({ tabId, panelId })
</script>

<template>
  <div class="ui-tabs">
    <div ref="list" class="list" role="tablist" :aria-label="label" @keydown="onKey">
      <button v-for="tab in tabs" :id="tabId(tab.value)" :key="tab.value" type="button" role="tab"
        :aria-selected="tab.value === model" :aria-controls="panelId(tab.value)" :tabindex="tab.value === model ? 0 : -1"
        @click="model = tab.value">
        <component :is="tab.icon" v-if="tab.icon" class="icon" aria-hidden="true" />
        {{ tab.label }}
      </button>
      <span v-if="indicator.ready" class="indicator" aria-hidden="true"
        :style="{ transform: `translateX(${indicator.left}px) scaleX(${indicator.width})` }"></span>
    </div>
    <div v-for="tab in tabs" v-show="tab.value === model" :id="panelId(tab.value)" :key="tab.value" class="panel" role="tabpanel"
      :aria-labelledby="tabId(tab.value)" tabindex="0">
      <slot v-if="tab.value === model" :name="tab.value" />
    </div>
  </div>
</template>

<style scoped>
.list {
  position: relative; display: flex; gap: var(--space-1); overflow-x: auto; border-bottom: 1px solid var(--border);
  scrollbar-width: none;
}
[role=tab] {
  display: inline-flex; align-items: center; gap: .45rem; min-height: 42px; padding: 0 var(--space-3); border: 0;
  border-radius: var(--radius-sm) var(--radius-sm) 0 0; color: var(--text-muted); background: transparent;
  font-weight: 650; font-size: var(--font-size-sm); white-space: nowrap;
  transition: color var(--motion-base) var(--ease-out), background-color var(--motion-base) var(--ease-out);
}
[role=tab]:hover { color: var(--text); background-color: color-mix(in oklab, var(--accent) 6%, transparent); }
[role=tab][aria-selected=true] { color: var(--text); }
[role=tab]:focus-visible { outline-offset: -3px; }
.icon { width: 16px; height: 16px; }
.indicator {
  position: absolute; bottom: 0; left: 0; width: 1px; height: 2px; background-color: var(--accent);
  transform-origin: left; transition: transform var(--motion-base) var(--ease-out);
}
.panel { padding-top: var(--space-5); }
.panel:focus-visible { outline-offset: 4px; }
</style>
