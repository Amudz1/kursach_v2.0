<script setup lang="ts">
import { computed } from 'vue'
import type { Statistics } from '../types'
const props = defineProps<{ data: Statistics; admin?: boolean }>()
const number = new Intl.NumberFormat('ru')
const percent = new Intl.NumberFormat('ru', { style: 'percent', maximumFractionDigits: 0 })
const attention = computed(() => props.admin ? 'Активных пользователей' : 'Токенов доступно сегодня')
const activity = computed(() => props.admin ? number.format(props.data.activeUsers) : props.data.usage?.remainingTokens ?? '∞')
</script>

<template>
  <div class="stats">
    <div class="stat-strip">
      <div><strong>{{ number.format(data.requests) }}</strong><span>Запросов за период</span></div>
      <div><strong>{{ activity }}</strong><span>{{ attention }}</span></div>
      <div><strong>{{ data.positiveShare === null ? '—' : percent.format(data.positiveShare) }}</strong><span>Полезных ответов</span></div>
      <div><strong>{{ number.format(data.promptTokens + data.completionTokens) }}</strong><span>Токенов всего</span></div>
    </div>
    <div class="form-grid">
      <section class="fieldset stack"><h3>Использованные персоны</h3>
        <dl v-if="data.byPersona.length" class="context-list"><div v-for="row in data.byPersona" :key="row.label"><dt>{{ row.label }}</dt><dd>{{ row.count }}</dd></div></dl>
        <p v-else class="muted">За этот период данных нет.</p>
      </section>
      <section class="fieldset stack"><h3>Использованные модели</h3>
        <dl v-if="data.byModel.length" class="context-list"><div v-for="row in data.byModel" :key="row.label"><dt>{{ row.label }}</dt><dd>{{ row.count }}</dd></div></dl>
        <p v-else class="muted">За этот период данных нет.</p>
      </section>
    </div>
    <p v-if="admin" class="help">Токены запроса: {{ number.format(data.promptTokens) }} · Токены ответа: {{ number.format(data.completionTokens) }} · Средняя оценка: {{ data.meanRating?.toFixed(2) ?? '—' }}</p>
  </div>
</template>
