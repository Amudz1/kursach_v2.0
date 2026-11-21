<script setup lang="ts">
import { computed } from 'vue'
import { Activity, Gauge, MessagesSquare, ThumbsUp, Users, Zap } from 'lucide-vue-next'
import { categoryFor } from '../appearance/entities'
import type { Statistics } from '../types'
const props = defineProps<{ data: Statistics; admin?: boolean }>()
const number = new Intl.NumberFormat('ru')
const percent = new Intl.NumberFormat('ru', { style: 'percent', maximumFractionDigits: 0 })
const attention = computed(() => props.admin ? 'Активных пользователей' : 'Токенов доступно сегодня')
const activity = computed(() => props.admin ? number.format(props.data.activeUsers) : props.data.usage?.remainingTokens ?? '∞')
const tiles = computed(() => [
  { value: number.format(props.data.requests), label: 'Запросов за период', icon: MessagesSquare,
    series: props.data.byPersona.map((row) => row.count) },
  { value: activity.value, label: attention.value, icon: props.admin ? Users : Gauge,
    series: props.admin ? props.data.byModel.map((row) => row.count) : [props.data.usage?.usedTokensToday, props.data.usage?.remainingTokens] },
  { value: props.data.positiveShare === null ? '—' : percent.format(props.data.positiveShare), label: 'Полезных ответов', icon: ThumbsUp,
    series: props.data.positiveShare === null ? [] : [1 - props.data.positiveShare, props.data.positiveShare] },
  { value: number.format(props.data.promptTokens + props.data.completionTokens), label: 'Токенов всего', icon: Zap,
    series: [props.data.promptTokens, props.data.completionTokens] },
])
function barHeight(value: number | null | undefined, series: (number | null | undefined)[]) {
  return `${Math.round((value ?? 0) / Math.max(1, ...series.map((point) => point ?? 0)) * 100)}%`
}
const charts = computed(() => [
  { title: 'Использованные персоны', rows: props.data.byPersona },
  { title: 'Использованные модели', rows: props.data.byModel },
].map((chart) => ({ ...chart, max: Math.max(1, ...chart.rows.map((row) => row.count)) })))
</script>

<template>
  <div class="stats">
    <div class="stat-strip">
      <div v-for="tile in tiles" :key="tile.label" class="stat-tile">
        <component :is="tile.icon" class="stat-icon" aria-hidden="true" />
        <strong>{{ tile.value }}</strong><span>{{ tile.label }}</span>
        <div v-if="tile.series.some(Boolean)" class="stat-spark" aria-hidden="true">
          <i v-for="(point, index) in tile.series.slice(0, 12)" :key="index" :style="{ height: barHeight(point, tile.series) }"></i>
        </div>
      </div>
    </div>
    <div class="form-grid">
      <section v-for="chart in charts" :key="chart.title" class="fieldset stack">
        <h3 class="actions"><Activity class="stat-icon" aria-hidden="true" />{{ chart.title }}</h3>
        <dl v-if="chart.rows.length" class="context-list stat-chart">
          <div v-for="row in chart.rows" :key="row.label">
            <dt>{{ row.label }}</dt><dd>{{ number.format(row.count) }}</dd>
            <dd class="chart-track" aria-hidden="true"><span :style="{ width: `${row.count / chart.max * 100}%`, backgroundColor: `var(--${categoryFor(row.label)})` }"></span></dd>
          </div>
        </dl>
        <p v-else class="muted">За этот период данных нет.</p>
      </section>
    </div>
    <p v-if="admin" class="help">Токены запроса: {{ number.format(data.promptTokens) }} · Токены ответа: {{ number.format(data.completionTokens) }} · Средняя оценка: {{ data.meanRating?.toFixed(2) ?? '—' }}</p>
  </div>
</template>

<style scoped>
.stat-strip { border-radius: var(--radius-md); overflow: hidden; }
.stat-icon { width: 18px; height: 18px; color: var(--accent); }
.stat-tile { min-height: 122px; }
.stat-spark { display: flex; gap: 3px; align-items: end; width: 100%; height: 21px; }
.stat-spark i { width: 7px; background: var(--accent-2); border-radius: var(--radius-xs); }
.stat-chart > div { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: var(--space-1); }
.stat-chart dt { overflow-wrap: anywhere; }
.chart-track { grid-column: 1 / -1; height: 7px; background: var(--surface-2); border-radius: var(--radius-full); }
.chart-track span { display: block; height: 100%; border-radius: inherit; }
</style>
