<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watchPostEffect } from 'vue'
import { markdown } from '../markdown'
import { useToast } from './ui/use-toast'

const props = defineProps<{ content: string }>()
const html = computed(() => markdown(props.content))
const root = ref<HTMLElement>()
const toast = useToast()
const timers = new Map<HTMLButtonElement, number>()
const displayLanguage: Record<string, string> = {
  csharp: 'C#', javascript: 'JavaScript', typescript: 'TypeScript', json: 'JSON', sql: 'SQL', bash: 'Bash',
}

watchPostEffect(() => {
  void html.value
  for (const pre of root.value?.querySelectorAll('pre') ?? []) {
    const code = pre.querySelector('code')
    if (!code || pre.parentElement?.classList.contains('code-frame')) continue
    const frame = document.createElement('div')
    frame.className = 'code-frame'
    const toolbar = document.createElement('div')
    toolbar.className = 'code-toolbar'
    const label = document.createElement('span')
    const language = code.dataset.language ?? ''
    label.textContent = Object.hasOwn(displayLanguage, language) ? displayLanguage[language] : 'Текст'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = 'Копировать код'
    button.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(code.textContent ?? '')
        if (!button.isConnected) return
        window.clearTimeout(timers.get(button))
        button.textContent = '✓ Скопировано'
        timers.set(button, window.setTimeout(() => {
          button.textContent = 'Копировать код'
          timers.delete(button)
        }, 1500))
      } catch { toast.error('Не удалось скопировать код.') }
    })
    toolbar.append(label, button)
    pre.before(frame)
    frame.append(toolbar, pre)
  }
})
onBeforeUnmount(() => {
  for (const timer of timers.values()) window.clearTimeout(timer)
})
</script>

<template>
  <div ref="root" class="markdown" v-html="html"></div>
</template>

<style scoped>
.markdown :deep(.code-frame) { overflow: hidden; border: 1px solid var(--border); border-radius: var(--radius-md); }
.markdown :deep(.code-toolbar) { display: flex; justify-content: space-between; align-items: center; gap: var(--space-2);
  padding-inline: var(--space-3); background: var(--surface-2); border-bottom: 1px solid var(--border); font-size: var(--font-size-xs); }
.markdown :deep(.code-toolbar button) { min-height: 40px; padding: 0 var(--space-2); color: var(--text);
  border: 0; background: transparent; border-radius: var(--radius-sm); }
.markdown :deep(.code-toolbar button:hover) { background: var(--surface-3); }
.markdown :deep(.code-frame pre) { margin: 0; border-radius: 0; }
</style>
