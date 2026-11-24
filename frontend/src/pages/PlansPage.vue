<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Check, Star } from 'lucide-vue-next'
import { api, explain } from '../api'
import { useSession } from '../session'
import UiBadge from '../components/ui/UiBadge.vue'
import UiSkeleton from '../components/ui/UiSkeleton.vue'
import type { Plan } from '../types'

const session = useSession()
const plans = ref<Plan[]>([])
const comparing = ref(false)
const recommended = computed(() => [...plans.value].filter((plan) => plan.durationDays && plan.price > 0)
  .sort((left, right) => left.price - right.price)[0]?.id)
const selected = ref<Plan>()
const dialog = ref<HTMLDialogElement>()
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const formError = ref('')
const status = ref('')
const payment = reactive({ cardNumber: '', cardHolder: '', expireMonth: '', expireYear: '', cvv: '', requestId: '' })
const currency = new Intl.NumberFormat('ru', { style: 'currency', currency: 'RUB', maximumFractionDigits: 0 })

async function load() {
  error.value = ''; loading.value = true
  try { plans.value = await api<Plan[]>('/plans', { auth: false }) }
  catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}
function fill() {
  Object.assign(payment, { cardNumber: '4242 4242 4242 4242', cardHolder: 'TEST USER', expireMonth: '12',
    expireYear: String(new Date().getFullYear() + 2), cvv: '123' })
}
function choose(plan: Plan) {
  selected.value = plan; formError.value = ''
  Object.assign(payment, { cardNumber: '', cardHolder: '', expireMonth: '', expireYear: '', cvv: '', requestId: crypto.randomUUID() })
  dialog.value?.showModal()
}
async function buy() {
  if (!selected.value) return
  formError.value = ''; busy.value = true
  try {
    await api('/subscriptions', { method: 'POST', body: { ...payment, planId: selected.value.id } })
    const name = selected.value.name
    dialog.value?.close()
    await session.reload()
    status.value = `Тариф «${name}» подключён. Оплата была демонстрационной.`
  } catch (reason) { formError.value = explain(reason); if (!dialog.value?.open) error.value = formError.value }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading"><h1>Тарифы</h1><p>Сравните дневной бюджет токенов, доступные модели и объём памяти.</p></header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button class="text-button" @click="load">Повторить</button></p>
    <p v-if="status" class="notice success" role="status">{{ status }}</p>
    <div v-if="loading" role="status"><span class="sr-only">Загружаем тарифы…</span><UiSkeleton :lines="3" /></div>
    <button v-if="plans.length" type="button" class="compare-toggle" :class="comparing ? 'secondary' : 'primary'"
      :aria-expanded="comparing" @click="comparing = !comparing">{{ comparing ? 'К списку тарифов' : 'Сравнить тарифы' }}</button>
    <div v-if="comparing" class="plan-comparison" tabindex="0" aria-label="Сравнение тарифов">
      <table>
        <thead><tr><th scope="col">Возможность</th><th v-for="plan in plans" :key="plan.id" scope="col">{{ plan.name }}</th></tr></thead>
        <tbody>
          <tr><th scope="row">Стоимость</th><td v-for="plan in plans" :key="plan.id">{{ currency.format(plan.price) }}</td></tr>
          <tr><th scope="row">Токены в день</th><td v-for="plan in plans" :key="plan.id">{{ plan.dailyTokenLimit?.toLocaleString('ru-RU') ?? 'Безлимит' }}</td></tr>
          <tr><th scope="row">Свои персоны</th><td v-for="plan in plans" :key="plan.id">{{ plan.maxPersonas }}</td></tr>
          <tr><th scope="row">Факты памяти</th><td v-for="plan in plans" :key="plan.id">{{ plan.maxMemories }}</td></tr>
          <tr><th scope="row">Вложения</th><td v-for="plan in plans" :key="plan.id">{{ Math.round(plan.maxAttachmentBytes / 1048576) }} МБ</td></tr>
          <tr><th scope="row">Доступные модели</th><td v-for="plan in plans" :key="plan.id">{{ plan.allowedModels.join(', ') }}</td></tr>
          <tr><th scope="row">Подключение</th><td v-for="plan in plans" :key="plan.id">
            <UiBadge v-if="plan.code === session.account?.usage.planCode" tone="success">Ваш тариф</UiBadge>
            <button v-else-if="plan.durationDays" type="button" class="secondary" @click="choose(plan)">Подключить</button>
          </td></tr>
        </tbody>
      </table>
    </div>
    <div v-else class="plan-board">
      <article v-for="plan in plans" :key="plan.id" class="plan-sheet"
        :class="{ current: plan.code === session.account?.usage.planCode, recommended: plan.id === recommended }">
        <div class="actions">
          <UiBadge v-if="plan.code === session.account?.usage.planCode" tone="success" :icon="Check">Ваш тариф</UiBadge>
          <UiBadge v-if="plan.id === recommended" tone="accent-2" :icon="Star">Рекомендуем</UiBadge>
        </div>
        <h2>{{ plan.name }}</h2>
        <p class="price">{{ currency.format(plan.price) }} <span v-if="plan.durationDays">/ {{ plan.durationDays }} дн.</span></p>
        <dl class="context-list">
          <div><dt>Токены в день</dt><dd>{{ plan.dailyTokenLimit?.toLocaleString('ru-RU') ?? 'Безлимит' }}</dd></div>
          <div><dt>Свои персоны</dt><dd>{{ plan.maxPersonas }}</dd></div>
          <div><dt>Факты памяти</dt><dd>{{ plan.maxMemories }}</dd></div>
          <div><dt>Вложения</dt><dd>{{ Math.round(plan.maxAttachmentBytes / 1048576) }} МБ</dd></div>
        </dl>
        <ul class="model-list" aria-label="Доступные модели"><li v-for="model in plan.allowedModels" :key="model"><Check aria-hidden="true" />{{ model }}</li></ul>
        <button v-if="plan.durationDays && plan.code !== session.account?.usage.planCode" class="secondary" @click="choose(plan)">Подключить</button>
      </article>
    </div>
    <dialog ref="dialog" aria-labelledby="pay-title" @close="Object.assign(payment, { cardNumber: '', cardHolder: '', expireMonth: '', expireYear: '', cvv: '' })">
      <form class="stack" @submit.prevent="buy">
        <h2 id="pay-title">Подключение тарифа</h2>
        <p>{{ selected?.name }} · {{ selected ? currency.format(selected.price) : '' }}. Используйте тестовые данные: деньги не списываются.</p>
        <button type="button" class="secondary self-start" @click="fill">Заполнить тестовыми данными</button>
        <label>Номер тестовой карты <input v-model="payment.cardNumber" maxlength="19" inputmode="numeric" autocomplete="off" required></label>
        <label>Имя держателя <input v-model="payment.cardHolder" maxlength="100" autocomplete="off" required></label>
        <div class="form-grid">
          <label>Месяц <input v-model="payment.expireMonth" maxlength="2" inputmode="numeric" required></label>
          <label>Год <input v-model="payment.expireYear" maxlength="4" inputmode="numeric" required></label>
          <label>CVV <input v-model="payment.cvv" maxlength="4" inputmode="numeric" autocomplete="off" required></label>
        </div>
        <p v-if="formError" class="notice danger" role="alert">{{ formError }}</p>
        <div class="actions"><button type="button" class="secondary" @click="dialog?.close()" :disabled="busy">Отмена</button>
          <button type="submit" class="primary" :disabled="busy">{{ busy ? 'Подключаем…' : 'Подтвердить' }}</button></div>
      </form>
    </dialog>
  </section>
</template>

<style scoped>
.compare-toggle { margin-bottom: var(--space-4); }
.plan-sheet.recommended { border-color: transparent; background: linear-gradient(var(--surface-1), var(--surface-1)) padding-box,
  linear-gradient(145deg, var(--accent), var(--accent-2)) border-box; box-shadow: var(--glow-md); }
.plan-sheet .model-list li { display: flex; align-items: flex-start; gap: var(--space-2); overflow-wrap: anywhere; }
.model-list svg { width: 15px; height: 15px; flex: 0 0 auto; color: var(--success); }
.plan-comparison { overflow-x: auto; border: 1px solid var(--border); border-radius: var(--radius-md); }
.plan-comparison table { border-collapse: collapse; width: 100%; background: var(--surface-1); }
.plan-comparison :is(th, td) { padding: var(--space-3); border-bottom: 1px solid var(--border); text-align: start; }
.plan-comparison thead, .plan-comparison tbody tr:hover { background: var(--surface-2); }
</style>
