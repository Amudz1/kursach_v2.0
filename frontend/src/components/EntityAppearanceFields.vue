<script setup lang="ts">
import { computed } from 'vue'
import { categoryColors, categoryLabels, entityColor, entityIcon, entityIconKeys, entityIcons } from '../appearance/entities'
import UiAvatar from './ui/UiAvatar.vue'
import UiColorPicker from './ui/UiColorPicker.vue'

const props = defineProps<{ name: string; seed: string; fallbackIcon: string }>()
const color = defineModel<string | null | undefined>('color')
const icon = defineModel<string | null | undefined>('icon')
const swatches = categoryColors.map((value) => ({ value, label: categoryLabels[value] }))
const selectedColor = computed(() => entityColor(color.value, props.seed))
const selectedIcon = computed(() => entityIcon(icon.value, props.fallbackIcon))
</script>

<template>
  <fieldset class="entity-style">
    <legend>Цвет и иконка</legend>
    <div class="actions">
      <UiAvatar :name="name || 'Предпросмотр'" :color="selectedColor" :icon="selectedIcon" size="lg" />
      <UiColorPicker :model-value="color ?? null" label="Цвет" :swatches="swatches" none-label="Автоматический цвет"
        @update:model-value="color = $event" />
    </div>
    <label>Иконка
      <select :value="icon ?? ''" @change="icon = ($event.target as HTMLSelectElement).value || null">
        <option value="">Стандартная</option>
        <option v-for="key in entityIconKeys" :key="key" :value="key">{{ entityIcons[key].label }}</option>
      </select>
    </label>
  </fieldset>
</template>

<style scoped>
.entity-style { display: grid; min-width: 0; gap: var(--space-3); margin: 0; padding: var(--space-3);
  border: 1px solid var(--border); border-radius: var(--radius-md); }
.entity-style legend { padding-inline: var(--space-1); font-weight: 650; }
</style>
