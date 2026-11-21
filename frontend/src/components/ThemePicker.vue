<script setup lang="ts">
import { setTheme, setThemeTint, theme, themeTint } from '../theme'
import type { Theme } from '../theme'
import { legacyFallbackSwatch as sampleTint, legacySwatches as swatches } from '../appearance/swatches'

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
    <fieldset class="palette">
      <legend>Цвет интерфейса</legend>
      <p class="help">Оттенок фона, панелей и акцентов.</p>
      <div class="palette-colors">
        <button v-for="swatch in swatches" :key="swatch.label" type="button" class="palette-choice"
          :aria-pressed="themeTint === swatch.value" @click="setThemeTint(swatch.value)">
          <span class="swatch" :style="{ '--swatch': swatch.value ?? undefined }" aria-hidden="true"></span>
          {{ swatch.label }}
        </button>
      </div>
      <label class="custom-color">
        <input type="color" :value="themeTint ?? sampleTint" aria-label="Свой цвет"
          @input="setThemeTint(($event.target as HTMLInputElement).value, true)"
          @change="setThemeTint(($event.target as HTMLInputElement).value)">
        <span>Свой цвет <small v-if="themeTint">{{ themeTint }}</small></span>
      </label>
    </fieldset>
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
.palette { display: grid; gap: .55rem; min-width: 0; margin: .95rem 0 0; padding: .55rem 0 0;
  border: 0; border-top: 1px solid var(--rule); }
.palette legend { padding: 0 .55rem 0 0; font-size: .88rem; }
.palette-colors { display: flex; flex-wrap: wrap; gap: .35rem; }
.palette-choice, .custom-color { display: inline-flex; align-items: center; gap: .4rem; justify-self: start;
  min-height: 36px; padding: .32rem .52rem; border: 1px solid var(--rule); border-radius: 7px;
  background: var(--inset); color: var(--ink); font-size: .79rem; font-weight: 620; cursor: pointer; }
.palette-choice:hover, .palette-choice[aria-pressed="true"], .custom-color:hover { background: var(--pale); border-color: var(--accent); }
.swatch { width: .8rem; height: .8rem; flex: 0 0 auto; border-radius: 50%;
  background: var(--swatch, var(--dark-rule)); border: 1px solid var(--dark-rule); }
.custom-color input { width: 1.55rem; height: 1.45rem; padding: .11rem; border-radius: 5px; cursor: pointer; }
.custom-color small { color: var(--muted); font: .76rem ui-monospace, Consolas, monospace; }
@media (max-width: 560px) { .theme-choices { grid-template-columns: 1fr; } }
</style>
