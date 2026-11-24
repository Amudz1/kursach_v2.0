<script setup lang="ts">
import { useId } from 'vue'
import { ChevronDown } from 'lucide-vue-next'

// A native <select>: the keyboard and screen readers already handle it everywhere.
type Value = string | number
const model = defineModel<Value>()
withDefaults(defineProps<{
  label: string
  options: readonly { value: Value; label: string; disabled?: boolean }[]
  hint?: string
  disabled?: boolean
  hideLabel?: boolean
}>(), {})
defineOptions({ inheritAttrs: false })
const id = `select-${useId()}`
</script>

<template>
  <div class="ui-field">
    <label :for="id" :class="{ 'sr-only': hideLabel }">{{ label }}</label>
    <div class="control">
      <select :id="id" v-model="model" v-bind="$attrs" :disabled="disabled" :aria-describedby="hint ? `${id}-hint` : undefined">
        <option v-for="option in options" :key="option.value" :value="option.value" :disabled="option.disabled">{{ option.label }}</option>
      </select>
      <ChevronDown class="chevron" aria-hidden="true" />
    </div>
    <p v-if="hint" :id="`${id}-hint`" class="message">{{ hint }}</p>
  </div>
</template>

<style scoped>
.ui-field { display: grid; gap: var(--space-2); min-width: 0; }
label { font-weight: 600; font-size: var(--font-size-sm); }
.control { position: relative; display: flex; align-items: center; }
select {
  min-height: 42px; padding-right: 2.4rem; border-radius: var(--radius-md); appearance: none;
  transition: border-color var(--motion-base) var(--ease-out), box-shadow var(--motion-base) var(--ease-out);
}
select:focus-visible { outline: none; border-color: var(--accent); box-shadow: var(--glow-sm); }
@media (hover: hover) { select:not(:disabled):hover { border-color: var(--text-muted); } }
.chevron { position: absolute; right: .75rem; width: 16px; height: 16px; color: var(--text-muted); pointer-events: none; }
.message { color: var(--text-muted); font-size: var(--font-size-xs); }
</style>
