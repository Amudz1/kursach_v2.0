<script setup lang="ts">
// Dev-only page (/dev/ui) that shows every library component in each state (ТЗ 8, этап 2).
import { ref } from 'vue'
import { Bot, Copy, MessageSquare, Plus, Search, Send, Settings, Sparkles, Trash2 } from 'lucide-vue-next'
import {
  UiAvatar, UiBadge, UiButton, UiCard, UiChip, UiColorPicker, UiDialog, UiDropdown, UiEmptyState, UiIconButton,
  UiInput, UiProgress, UiSegmented, UiSelect, UiSkeleton, UiSwitch, UiTabs, UiTextarea, useToast,
} from '../components/ui'
import { categoryColors, categoryLabels } from '../appearance/entities'
import { appearance, setAppearance, type ColorMode } from '../appearance'

const toast = useToast()
const mode = ref<ColorMode>(appearance.value.mode)
const density = ref<'compact' | 'normal' | 'spacious'>('normal')
const enabled = ref(true)
const text = ref('')
const note = ref('')
const password = ref('secret')
const model = ref('balanced')
const tab = ref('one')
const dialog = ref(false)
const color = ref<string | null>('cat-1')
const loading = ref(false)
const swatches = categoryColors.map((value) => ({ value, label: categoryLabels[value] }))

function changeMode(value: ColorMode) { mode.value = value; setAppearance({ mode: value }) }
function load() { loading.value = true; setTimeout(() => { loading.value = false; toast.success('Сохранено') }, 1200) }
</script>

<template>
  <div class="showcase">
    <h1>Библиотека компонентов</h1>
    <UiSegmented :model-value="mode" label="Тема" :options="[
      { value: 'system', label: 'Системная' }, { value: 'light', label: 'Светлая' }, { value: 'dark', label: 'Тёмная' },
    ]" @update:model-value="changeMode" />

    <UiCard>
      <h2>Кнопки</h2>
      <div class="row">
        <UiButton variant="primary" :icon="Send" :loading="loading" @click="load">Отправить</UiButton>
        <UiButton :icon="Plus">Вторичная</UiButton>
        <UiButton variant="ghost">Призрачная</UiButton>
        <UiButton variant="danger" :icon="Trash2">Удалить</UiButton>
        <UiButton variant="primary" disabled>Недоступна</UiButton>
        <UiButton size="sm">Маленькая</UiButton>
        <UiButton size="lg" variant="primary">Большая</UiButton>
      </div>
      <div class="row">
        <UiIconButton :icon="Copy" label="Копировать" />
        <UiIconButton :icon="Settings" label="Настройки" variant="solid" />
        <UiIconButton :icon="Send" label="Отправить" variant="primary" />
        <UiIconButton :icon="Trash2" label="Удалить" variant="danger" />
        <UiIconButton :icon="Sparkles" label="Закрепить" :pressed="enabled" @click="enabled = !enabled" />
      </div>
    </UiCard>

    <UiCard>
      <h2>Поля ввода</h2>
      <div class="grid">
        <UiInput v-model="text" label="Поиск" :icon="Search" placeholder="Найти диалог" hint="Ищет по названию" />
        <UiInput v-model="password" label="Пароль" type="password" reveal />
        <UiInput model-value="" label="С ошибкой" error="Заполните поле" />
        <UiSelect v-model="model" label="Модель" :options="[
          { value: 'fast', label: 'Быстрая' }, { value: 'balanced', label: 'Сбалансированная' }, { value: 'smart', label: 'Умная' },
        ]" />
        <UiTextarea v-model="note" label="Заметка" :maxlength="120" :auto-grow="200" placeholder="Растёт вместе с текстом" />
        <div class="stack">
          <UiSwitch v-model="enabled" label="Подсветка" description="Мягкое свечение у кнопок" />
          <UiSegmented v-model="density" label="Плотность" :options="[
            { value: 'compact', label: 'Плотно' }, { value: 'normal', label: 'Обычно' }, { value: 'spacious', label: 'Просторно' },
          ]" />
        </div>
      </div>
    </UiCard>

    <UiCard>
      <h2>Метки, чипы, аватары</h2>
      <div class="row">
        <UiBadge>Обычная</UiBadge><UiBadge tone="accent">Акцент</UiBadge><UiBadge tone="success" dot>Активен</UiBadge>
        <UiBadge tone="warning" dot>Мало токенов</UiBadge><UiBadge tone="danger" dot>Ошибка</UiBadge>
        <UiBadge tone="info">Инфо</UiBadge><UiBadge category="cat-4">Проект</UiBadge>
      </div>
      <div class="row">
        <UiChip arrow>Объясни код</UiChip><UiChip :icon="MessageSquare" category="cat-2">С иконкой</UiChip>
        <UiChip :pressed="enabled" @click="enabled = !enabled">Переключатель</UiChip><UiChip disabled>Недоступен</UiChip>
      </div>
      <div class="row">
        <UiAvatar name="Анна" size="xs" /><UiAvatar name="Борис" size="sm" /><UiAvatar name="Вера" />
        <UiAvatar name="Ассистент" :icon="Bot" brand size="lg" /><UiAvatar name="Глеб" size="xl" />
      </div>
      <UiColorPicker v-model="color" label="Цвет проекта" :swatches="swatches" custom none-label="Без цвета" />
    </UiCard>

    <div class="grid">
      <UiCard interactive spotlight category="cat-3"><h2>Интерактивная</h2><p>Поднимается при наведении.</p></UiCard>
      <UiCard selected><h2>Выбранная</h2><p>Обводка акцентом.</p></UiCard>
      <UiCard loading />
    </div>

    <UiCard>
      <h2>Прогресс и загрузка</h2>
      <div class="stack">
        <UiProgress :value="72" label="Остаток токенов" meter show-value />
        <UiProgress :value="30" label="Остаток токенов" meter show-value />
        <UiProgress :value="8" label="Остаток токенов" meter show-value />
        <UiProgress :value="0" label="Загрузка" indeterminate />
        <div class="row"><UiProgress :value="64" label="Кольцо" variant="ring" show-value /><UiSkeleton :lines="2" width="14rem" /><UiSkeleton circle width="40px" height="40px" /></div>
      </div>
    </UiCard>

    <UiCard>
      <UiTabs v-model="tab" label="Пример вкладок" :tabs="[{ value: 'one', label: 'Первая' }, { value: 'two', label: 'Вторая' }, { value: 'three', label: 'Третья' }]">
        <template #one><p>Содержимое первой вкладки.</p></template>
        <template #two><p>Содержимое второй вкладки.</p></template>
        <template #three><p>Содержимое третьей вкладки.</p></template>
      </UiTabs>
    </UiCard>

    <UiCard>
      <h2>Меню, диалог, уведомления</h2>
      <div class="row">
        <UiDropdown label="Действия">
          <template #trigger="{ attrs }"><UiButton v-bind="attrs" :icon-right="Settings">Меню</UiButton></template>
          <template #default="{ close }">
            <button type="button" role="menuitem" @click="close()">Переименовать</button>
            <button type="button" role="menuitem" @click="close()">Закрепить</button>
            <div role="separator"></div>
            <button type="button" role="menuitem" class="danger" @click="close()">Удалить</button>
          </template>
        </UiDropdown>
        <UiButton @click="dialog = true">Открыть диалог</UiButton>
        <UiButton @click="toast.success('Готово')">Успех</UiButton>
        <UiButton @click="toast.show('Новая версия', 'info', { action: { label: 'Обновить', run: () => {} } })">Инфо</UiButton>
        <UiButton @click="toast.error('Не удалось сохранить')">Ошибка</UiButton>
      </div>
    </UiCard>

    <UiEmptyState :icon="MessageSquare" title="Пока пусто" text="Начните новый диалог, и он появится здесь.">
      <UiButton variant="primary" :icon="Plus">Новый диалог</UiButton>
    </UiEmptyState>

    <UiDialog v-model="dialog" title="Удалить диалог?" description="Это действие нельзя отменить.">
      <p>Диалог и все сообщения будут удалены.</p>
      <template #footer>
        <UiButton variant="ghost" @click="dialog = false">Отмена</UiButton>
        <UiButton variant="danger" @click="dialog = false">Удалить</UiButton>
      </template>
    </UiDialog>
  </div>
</template>

<style scoped>
.showcase { display: grid; width: min(100% - 2rem, 72rem); margin-inline: auto; gap: var(--space-5); padding-block: var(--space-6); }
h2 { margin-bottom: var(--space-3); font-size: var(--font-size-lg); }
.row { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-3); margin-bottom: var(--space-3); }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr)); gap: var(--space-4); }
.stack { display: grid; gap: var(--space-3); }
</style>
