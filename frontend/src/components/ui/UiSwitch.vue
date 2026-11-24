<script setup lang="ts">
import { useId } from 'vue'

// A checkbox with role="switch": Space toggles it, and the state is read out as «включено/выключено».
const model = defineModel<boolean>({ default: false })
defineProps<{ label: string; description?: string; disabled?: boolean }>()
const id = `switch-${useId()}`
</script>

<template>
  <label class="ui-switch" :class="{ disabled }" :for="id">
    <input :id="id" v-model="model" type="checkbox" role="switch" :disabled="disabled"
      :aria-describedby="description ? `${id}-description` : undefined" />
    <span class="track" aria-hidden="true"><span class="thumb"></span></span>
    <span class="text">
      <span class="label">{{ label }}</span>
      <span v-if="description" :id="`${id}-description`" class="description">{{ description }}</span>
    </span>
  </label>
</template>

<style scoped>
.ui-switch { display: flex !important; align-items: flex-start; gap: var(--space-3); cursor: pointer; font-weight: 400; }
.ui-switch.disabled { cursor: not-allowed; opacity: .55; }
input { position: absolute; width: 1px; height: 1px; opacity: 0; pointer-events: none; }
.track {
  position: relative; flex: 0 0 auto; width: 40px; height: 24px; margin-top: 1px; border: 1px solid var(--dark-rule);
  border-radius: var(--radius-full); background-color: var(--surface-2);
  transition: background-color var(--motion-base) var(--ease-out), border-color var(--motion-base) var(--ease-out), box-shadow var(--motion-base);
}
.thumb {
  position: absolute; top: 3px; left: 3px; width: 16px; height: 16px; border-radius: var(--radius-full);
  background-color: var(--text-muted); box-shadow: var(--elevation-1);
  transition: transform var(--motion-base) var(--ease-spring), background-color var(--motion-base);
}
/* The thumb position and the fill both change, so the state does not depend on colour alone (ТЗ 10). */
input:checked + .track { border-color: var(--accent); background-color: var(--accent); }
input:checked + .track .thumb { transform: translateX(16px); background-color: var(--on-accent); }
input:focus-visible + .track { outline: 3px solid var(--focus); outline-offset: 2px; box-shadow: var(--glow-sm); }
.ui-switch:not(.disabled):active .thumb { transform: scale(.9); }
.ui-switch:not(.disabled):active input:checked + .track .thumb { transform: translateX(16px) scale(.9); }
.text { display: grid; gap: 2px; }
.label { font-weight: 600; font-size: var(--font-size-sm); line-height: 1.4; }
.description { color: var(--text-muted); font-size: var(--font-size-xs); line-height: 1.4; }
</style>
