<script setup lang="ts">
import { setTheme, theme } from '../theme'
import type { Theme } from '../theme'

const choices: { value: Theme; label: string; description: string }[] = [
  { value: 'system', label: 'Как в браузере', description: 'Автоматически следует настройке устройства' },
  { value: 'light', label: 'Светлая', description: 'Тёплые бежевые оттенки' },
  { value: 'dark', label: 'Тёмная', description: 'Приглушённый тёмный фон' },
]
</script>

<template>
  <fieldset class="theme-picker">
    <legend>Тема оформления</legend>
    <div class="theme-choices">
      <label v-for="choice in choices" :key="choice.value" class="theme-choice" :class="{ chosen: theme === choice.value }">
        <input type="radio" name="budz-theme" :value="choice.value" :checked="theme === choice.value"
          :aria-label="choice.label" @change="setTheme(choice.value)">
        <span><strong>{{ choice.label }}</strong><small>{{ choice.description }}</small></span>
      </label>
    </div>
  </fieldset>
</template>

<style scoped>
.theme-picker { min-width: 0; margin: 0; padding: .95rem 1rem 1.1rem; border: 1px solid var(--rule);
  border-radius: 10px; background: var(--sheet); }
.theme-picker legend { padding: 0 .4rem; font-weight: 700; }
.theme-choices { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .45rem; }
.theme-choice { display: flex; align-items: start; gap: .5rem; min-width: 0; padding: .65rem;
  border: 1px solid var(--rule); border-radius: 8px; background: var(--inset); cursor: pointer; }
.theme-choice:hover, .theme-choice.chosen { border-color: var(--accent); background: var(--pale); }
.theme-choice input { width: 1rem; height: 1rem; flex: 0 0 auto; padding: 0; margin: .12rem 0 0; accent-color: var(--accent); }
.theme-choice strong, .theme-choice small { display: block; }
.theme-choice small { margin-top: .2rem; color: var(--muted); font-size: .77rem; font-weight: 400; line-height: 1.35; }
@media (max-width: 560px) { .theme-choices { grid-template-columns: 1fr; } }
</style>
