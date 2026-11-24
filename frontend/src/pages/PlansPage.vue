<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { api, explain } from '../api'
import { useSession } from '../session'
import type { Plan } from '../types'

const session = useSession()
const plans = ref<Plan[]>([])
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
    <p v-if="loading" class="muted" role="status">Загружаем тарифы…</p>
    <div class="plan-board">
      <article v-for="plan in plans" :key="plan.id" class="plan-sheet" :class="{ current: plan.code === session.account?.usage.planCode }">
        <span class="eyebrow">{{ plan.code }}</span>
        <h2>{{ plan.name }}</h2>
        <p class="price">{{ currency.format(plan.price) }} <span v-if="plan.durationDays">/ {{ plan.durationDays }} дн.</span></p>
        <dl class="context-list">
          <div><dt>Токены в день</dt><dd>{{ plan.dailyTokenLimit?.toLocaleString('ru-RU') ?? 'Безлимит' }}</dd></div>
          <div><dt>Свои персоны</dt><dd>{{ plan.maxPersonas }}</dd></div>
          <div><dt>Факты памяти</dt><dd>{{ plan.maxMemories }}</dd></div>
          <div><dt>Вложения</dt><dd>{{ Math.round(plan.maxAttachmentBytes / 1048576) }} МБ</dd></div>
        </dl>
        <ul class="model-list" aria-label="Доступные модели"><li v-for="model in plan.allowedModels" :key="model">{{ model }}</li></ul>
        <button v-if="plan.durationDays && plan.code !== session.account?.usage.planCode" class="primary" @click="choose(plan)">Подключить</button>
        <p v-else-if="plan.code === session.account?.usage.planCode" class="muted">Текущий тариф</p>
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
