<script setup lang="ts">
import { CircleCheck, CircleX, Info, TriangleAlert, X } from 'lucide-vue-next'
import { useToast } from './use-toast'

// Mounted once in App.vue. Two live regions: errors interrupt, everything else waits its turn.
const { toasts, dismiss } = useToast()
const icons = { success: CircleCheck, info: Info, warning: TriangleAlert, danger: CircleX }
const labels = { success: 'Готово', info: 'Сообщение', warning: 'Внимание', danger: 'Ошибка' }
</script>

<template>
  <div class="ui-toasts">
    <div class="region" role="status" aria-live="polite">
      <TransitionGroup name="ui-toast">
        <div v-for="toast in toasts.filter((item) => item.tone !== 'danger')" :key="toast.id" class="toast" :class="toast.tone">
          <component :is="icons[toast.tone]" class="icon" aria-hidden="true" />
          <p><span class="sr-only">{{ labels[toast.tone] }}: </span>{{ toast.text }}</p>
          <button v-if="toast.action" type="button" class="action" @click="toast.action.run(); dismiss(toast.id)">{{ toast.action.label }}</button>
          <button type="button" class="close" aria-label="Закрыть уведомление" @click="dismiss(toast.id)"><X aria-hidden="true" /></button>
        </div>
      </TransitionGroup>
    </div>
    <div class="region" role="alert" aria-live="assertive">
      <TransitionGroup name="ui-toast">
        <div v-for="toast in toasts.filter((item) => item.tone === 'danger')" :key="toast.id" class="toast danger">
          <CircleX class="icon" aria-hidden="true" />
          <p><span class="sr-only">{{ labels.danger }}: </span>{{ toast.text }}</p>
          <button type="button" class="close" aria-label="Закрыть уведомление" @click="dismiss(toast.id)"><X aria-hidden="true" /></button>
        </div>
      </TransitionGroup>
    </div>
  </div>
</template>

<style scoped>
.ui-toasts {
  position: fixed; z-index: 70; right: var(--space-4); bottom: var(--space-4); display: grid; gap: var(--space-2);
  width: min(calc(100vw - 2rem), 24rem); pointer-events: none;
}
.region { display: grid; gap: var(--space-2); }
.toast {
  --toast-color: var(--success);
  display: flex; align-items: center; gap: var(--space-3); padding: var(--space-3) var(--space-2) var(--space-3) var(--space-4);
  border: 1px solid var(--border); border-left: 3px solid var(--toast-color); border-radius: var(--radius-lg);
  color: var(--text); background-color: var(--surface-1); box-shadow: var(--elevation-3); pointer-events: auto;
}
.info { --toast-color: var(--info); }
.warning { --toast-color: var(--warning); }
.danger { --toast-color: var(--danger); }
.icon { width: 19px; height: 19px; flex: 0 0 auto; color: var(--toast-color); }
p { flex: 1; font-weight: 550; font-size: var(--font-size-sm); line-height: 1.4; }
.action { padding: .3rem .5rem; border: 0; border-radius: var(--radius-sm); color: var(--accent); background: transparent; font-weight: 700; }
.action:hover { background-color: var(--surface-2); }
.close {
  display: grid; place-items: center; width: 32px; height: 32px; padding: 0; border: 0; border-radius: var(--radius-full);
  color: var(--text-muted); background-color: transparent; transition: background-color var(--motion-fast);
}
.close:hover { background-color: var(--surface-2); color: var(--text); }
.close svg { width: 16px; height: 16px; }
.ui-toast-enter-active, .ui-toast-leave-active { transition: opacity var(--motion-base) var(--ease-out), transform var(--motion-base) var(--ease-out); }
.ui-toast-enter-from { opacity: 0; transform: translateY(10px) scale(.98); }
.ui-toast-leave-to { opacity: 0; transform: translateX(16px); }
@media (max-width: 640px) { .ui-toasts { right: 1rem; left: 1rem; width: auto; } }
</style>
