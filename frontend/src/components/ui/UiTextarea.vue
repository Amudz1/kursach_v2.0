<script setup lang="ts">
import { computed, nextTick, onMounted, ref, useId, watch } from 'vue'

const model = defineModel<string>({ default: '' })
const props = withDefaults(defineProps<{
  label: string
  hint?: string
  error?: string
  placeholder?: string
  rows?: number
  maxlength?: number
  /** Grows with the text up to this height in pixels. */
  autoGrow?: number
  /** Shows the counter once the text passes this share of `maxlength`. */
  counterFrom?: number
  disabled?: boolean
  hideLabel?: boolean
}>(), { rows: 3, counterFrom: .8 })
defineOptions({ inheritAttrs: false })

const id = `textarea-${useId()}`
const element = ref<HTMLTextAreaElement>()
const showCounter = computed(() => !!props.maxlength && model.value.length >= props.maxlength * props.counterFrom)
const described = computed(() => [props.error && `${id}-error`, props.hint && `${id}-hint`, showCounter.value && `${id}-count`]
  .filter(Boolean).join(' ') || undefined)

function grow() {
  const node = element.value
  if (!node || !props.autoGrow) return
  node.style.height = 'auto'
  node.style.height = `${Math.min(node.scrollHeight + 2, props.autoGrow)}px`
}
watch(model, () => void nextTick(grow))
onMounted(grow)
defineExpose({ focus: () => element.value?.focus() })
</script>

<template>
  <div class="ui-field" :class="{ invalid: !!error }">
    <label :for="id" :class="{ 'sr-only': hideLabel }">{{ label }}</label>
    <textarea :id="id" ref="element" v-model="model" v-bind="$attrs" :rows="rows" :placeholder="placeholder" :maxlength="maxlength"
      :disabled="disabled" :aria-invalid="error ? true : undefined" :aria-describedby="described"
      :class="{ grow: autoGrow }"></textarea>
    <div v-if="error || hint || showCounter" class="footer">
      <p v-if="error" :id="`${id}-error`" class="message error-text">{{ error }}</p>
      <p v-else-if="hint" :id="`${id}-hint`" class="message">{{ hint }}</p>
      <p v-if="showCounter" :id="`${id}-count`" class="message counter" :class="{ full: model.length >= (maxlength ?? 0) }">
        {{ model.length.toLocaleString('ru-RU') }} / {{ maxlength?.toLocaleString('ru-RU') }}
      </p>
    </div>
  </div>
</template>

<style scoped>
.ui-field { display: grid; gap: var(--space-2); min-width: 0; }
label { font-weight: 600; font-size: var(--font-size-sm); }
textarea {
  border-radius: var(--radius-md); line-height: 1.45;
  transition: border-color var(--motion-base) var(--ease-out), box-shadow var(--motion-base) var(--ease-out);
}
textarea.grow { resize: none; overflow-y: auto; }
textarea:focus-visible { outline: none; border-color: var(--accent); box-shadow: var(--glow-sm); }
.invalid textarea { border-color: var(--danger); }
.footer { display: flex; justify-content: space-between; gap: var(--space-3); }
.message { color: var(--text-muted); font-size: var(--font-size-xs); line-height: 1.4; }
.error-text { color: var(--danger); font-weight: 600; }
.counter { margin-left: auto; font-variant-numeric: tabular-nums; }
.counter.full { color: var(--danger); font-weight: 650; }
</style>
