<script setup lang="ts">
import { Check } from 'lucide-vue-next'
import { requestInterfaceSwitch } from '../interface-switch'
import { useSession } from '../session'
import type { InterfaceVariant } from '../types'

defineProps<{ drawer?: boolean }>()
const emit = defineEmits<{ select: [] }>()
const session = useSession()
const options = [
  { value: 'current', label: 'Текущий Budz' },
  { value: 'focus', label: 'Фокус' },
] as const
function choose(value: InterfaceVariant) {
  emit('select')
  requestInterfaceSwitch(value)
}
</script>

<template>
  <div class="interface-options" :class="{ 'drawer-options': drawer }" role="group" aria-label="Интерфейс">
    <span class="interface-label" aria-hidden="true">Интерфейс</span>
    <button v-for="option in options" :key="option.value" type="button" :role="drawer ? undefined : 'menuitemradio'"
      :tabindex="drawer ? undefined : -1" :aria-checked="drawer ? undefined : session.interfaceVariant === option.value"
      :aria-pressed="drawer ? session.interfaceVariant === option.value : undefined" :disabled="session.interfaceSaving"
      @click="choose(option.value)">
      <Check v-if="session.interfaceVariant === option.value" aria-hidden="true" />{{ option.label }}
    </button>
  </div>
</template>

<style scoped>
.interface-options { display: grid; gap: var(--space-1); }
.interface-label { padding-inline: var(--space-3); color: var(--text-muted); font-size: var(--font-size-xs); }
.drawer-options { margin-block: var(--space-1); }
.drawer-options [aria-pressed="true"] { background: var(--panel-active); font-weight: 700; }
.drawer-options svg { width: 16px; height: 16px; }
</style>
