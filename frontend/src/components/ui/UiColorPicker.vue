<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { Check } from 'lucide-vue-next'
import { readHex } from '../../appearance/model'

// Swatches as a radio group plus a custom colour. Values are either hex colours or token names such as `cat-3`.
const model = defineModel<string | null>({ default: null })
const props = withDefaults(defineProps<{
  label: string
  swatches: readonly { value: string; label: string; css?: string }[]
  /** Adds a native colour input labelled «Свой цвет». */
  custom?: boolean
  /** An option for «no colour»/«as in the preset». */
  noneLabel?: string
  hideLabel?: boolean
}>(), { custom: false })

const name = `color-${useId()}`
const customId = `${name}-custom`
const isSwatch = computed(() => props.swatches.some((swatch) => swatch.value === model.value))
const customValue = ref(readHex(model.value) ?? '#3d5949')
watch(model, (value) => { const hex = readHex(value); if (hex && !isSwatch.value) customValue.value = hex })
const cssOf = (swatch: { value: string; css?: string }) => swatch.css ?? (swatch.value.startsWith('#') ? swatch.value : `var(--${swatch.value})`)
function pickCustom(event: Event) {
  const hex = readHex((event.target as HTMLInputElement).value)
  if (hex) { customValue.value = hex; model.value = hex }
}
</script>

<template>
  <fieldset class="ui-color-picker">
    <legend :class="{ 'sr-only': hideLabel }">{{ label }}</legend>
    <div class="swatches">
      <label v-if="noneLabel" class="swatch none" :title="noneLabel">
        <input v-model="model" type="radio" :name="name" :value="null" :aria-label="noneLabel" />
        <span class="chip" aria-hidden="true"><Check v-if="model === null" class="mark" /></span>
      </label>
      <label v-for="swatch in swatches" :key="swatch.value" class="swatch" :title="swatch.label">
        <input v-model="model" type="radio" :name="name" :value="swatch.value" :aria-label="swatch.label" />
        <span class="chip" :style="{ backgroundColor: cssOf(swatch) }" aria-hidden="true">
          <Check v-if="model === swatch.value" class="mark" />
        </span>
      </label>
      <label v-if="custom" class="custom" :for="customId" :class="{ active: model && !isSwatch }">
        <input :id="customId" type="color" :value="customValue" aria-label="Свой цвет" @input="pickCustom" @change="pickCustom" />
        <span>Свой цвет</span>
      </label>
    </div>
  </fieldset>
</template>

<style scoped>
.ui-color-picker { min-width: 0; margin: 0; padding: 0; border: 0; }
legend { margin-bottom: var(--space-2); padding: 0; font-weight: 600; font-size: var(--font-size-sm); }
.swatches { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); }
.swatch { position: relative; display: inline-grid !important; cursor: pointer; }
.swatch input { position: absolute; inset: 0; margin: 0; opacity: 0; cursor: pointer; }
.chip {
  display: grid; place-items: center; width: 30px; height: 30px; border-radius: var(--radius-full);
  box-shadow: inset 0 0 0 1px color-mix(in oklab, var(--text) 18%, transparent);
  transition: transform var(--motion-fast) var(--ease-spring), box-shadow var(--motion-base);
}
.none .chip { background: repeating-linear-gradient(-45deg, var(--surface-2) 0 4px, var(--surface-1) 4px 8px); color: var(--text); }
.mark { width: 16px; height: 16px; color: var(--on-fill); filter: drop-shadow(0 0 1px var(--c-ink-900)); }
.none .mark { color: var(--text); filter: none; }
.swatch:hover .chip { transform: scale(1.08); }
.swatch:active .chip { transform: scale(.94); }
/* The check mark, not just the ring colour, shows the selection (ТЗ 10). */
.swatch:has(input:checked) .chip { box-shadow: 0 0 0 2px var(--surface-1), 0 0 0 4px var(--text); }
.swatch:has(input:focus-visible) .chip { outline: 3px solid var(--focus); outline-offset: 4px; }
.custom {
  display: inline-flex !important; align-items: center; gap: var(--space-2); min-height: 34px; padding: 2px var(--space-3) 2px 2px;
  border: 1px solid var(--border); border-radius: var(--radius-full); font-weight: 600; font-size: var(--font-size-xs); cursor: pointer;
}
.custom.active { border-color: var(--text); box-shadow: inset 0 0 0 1px var(--text); }
.custom input { width: 28px; height: 28px; padding: 0; border: 0; border-radius: var(--radius-full); background: none; cursor: pointer; }
.custom input::-webkit-color-swatch-wrapper { padding: 0; }
.custom input::-webkit-color-swatch { border: 0; border-radius: 50%; }
.custom input::-moz-color-swatch { border: 0; border-radius: 50%; }
</style>
