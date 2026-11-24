<script setup lang="ts">
import { computed, ref, useId, type Component } from 'vue'
import { Eye, EyeOff } from 'lucide-vue-next'

const model = defineModel<string>({ default: '' })
const props = withDefaults(defineProps<{
  label: string
  type?: 'text' | 'email' | 'password' | 'search' | 'url' | 'number'
  icon?: Component
  hint?: string
  error?: string
  placeholder?: string
  autocomplete?: string
  required?: boolean
  disabled?: boolean
  maxlength?: number
  /** Hides the label visually but keeps it for assistive technology. */
  hideLabel?: boolean
  /** Adds a «show password» toggle for `type="password"`. */
  reveal?: boolean
}>(), { type: 'text' })
defineOptions({ inheritAttrs: false })

const id = `input-${useId()}`
const visible = ref(false)
const inputType = computed(() => (props.type === 'password' && visible.value ? 'text' : props.type))
const described = computed(() => [props.error && `${id}-error`, props.hint && `${id}-hint`].filter(Boolean).join(' ') || undefined)
</script>

<template>
  <div class="ui-field" :class="{ invalid: !!error, disabled }">
    <label :for="id" :class="{ 'sr-only': hideLabel }">{{ label }}</label>
    <div class="control">
      <component :is="icon" v-if="icon" class="lead" aria-hidden="true" />
      <input :id="id" v-model="model" v-bind="$attrs" :type="inputType" :placeholder="placeholder" :autocomplete="autocomplete"
        :required="required" :disabled="disabled" :maxlength="maxlength" :aria-invalid="error ? true : undefined"
        :aria-describedby="described" :class="{ 'has-icon': icon, 'has-toggle': reveal && type === 'password' }" />
      <button v-if="reveal && type === 'password'" type="button" class="reveal" :aria-pressed="visible"
        :aria-label="visible ? 'Скрыть пароль' : 'Показать пароль'" @click="visible = !visible">
        <component :is="visible ? EyeOff : Eye" aria-hidden="true" />
      </button>
    </div>
    <p v-if="error" :id="`${id}-error`" class="message error-text">{{ error }}</p>
    <p v-if="hint" :id="`${id}-hint`" class="message">{{ hint }}</p>
  </div>
</template>

<style scoped>
.ui-field { display: grid; gap: var(--space-2); min-width: 0; }
label { font-weight: 600; font-size: var(--font-size-sm); }
.control { position: relative; display: flex; align-items: center; }
input {
  min-height: 42px; border-radius: var(--radius-md);
  transition: border-color var(--motion-base) var(--ease-out), box-shadow var(--motion-base) var(--ease-out);
}
input:focus-visible { outline: none; border-color: var(--accent); box-shadow: var(--glow-sm); }
input:focus:not(:focus-visible) { border-color: var(--accent); }
@media (hover: hover) { input:not(:disabled):not(:focus):hover { border-color: var(--text-muted); } }
.has-icon { padding-left: 2.3rem; }
.has-toggle { padding-right: 2.8rem; }
.lead { position: absolute; left: .75rem; width: 17px; height: 17px; color: var(--text-muted); pointer-events: none; }
.reveal {
  position: absolute; right: .25rem; display: grid; place-items: center; width: 36px; height: 36px; padding: 0;
  border: 0; border-radius: var(--radius-full); color: var(--text-muted); background: transparent;
  transition: color var(--motion-base), background-color var(--motion-base);
}
.reveal:hover { color: var(--text); background-color: var(--surface-2); }
.reveal svg { width: 18px; height: 18px; }
.invalid input { border-color: var(--danger); }
.invalid input:focus-visible { box-shadow: 0 0 0 3px color-mix(in oklab, var(--danger) 35%, transparent); }
.message { color: var(--text-muted); font-size: var(--font-size-xs); line-height: 1.4; }
.error-text { color: var(--danger); font-weight: 600; }
.disabled { opacity: .6; }
</style>
