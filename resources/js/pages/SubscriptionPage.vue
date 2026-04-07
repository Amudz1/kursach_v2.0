<template>
  <div class="page">
    <div class="page-title">✨ Оформление подписки</div>
    <p class="page-subtitle">Выберите тарифный план и получите безлимитный доступ к AI</p>

    <!-- Шаг 1: Выбор плана -->
    <template v-if="step === 'plans'">
      <div v-if="auth.hasSubscription" class="alert alert-info" style="max-width:700px;margin-bottom:24px">
        ⭐ У вас уже есть активная подписка: <strong>{{ auth.user?.subscription?.plan_name }}</strong> до {{ auth.user?.subscription?.ends_at }}
      </div>

      <div class="plans-grid">
        <div
          v-for="plan in plans"
          :key="plan.id"
          class="plan-card"
          :class="{
            'plan-card--selected': selectedPlan?.id === plan.id,
            'plan-card--popular': plan.discount_percent >= 6
          }"
          @click="selectPlan(plan)"
        >
          <div v-if="plan.discount_percent >= 6" class="plan-badge">🔥 Выгодно</div>
          <div v-else-if="plan.discount_percent > 0" class="plan-badge plan-badge--blue">💡 Скидка</div>

          <div class="plan-content">
            <div class="plan-top">
              <div class="plan-name">{{ plan.name }}</div>

              <div class="plan-price-wrap">
                <span v-if="plan.discount_percent > 0" class="plan-price-old">
                  ${{ Number(plan.price).toFixed(2) }}
                </span>
                <span class="plan-price-final">
                  ${{ Number(plan.final_price ?? plan.price).toFixed(2) }}
                </span>
              </div>

              <div v-if="plan.discount_percent > 0" class="plan-discount">
                Скидка {{ Number(plan.discount_percent).toFixed(0) }}% — экономия ${{ Number(plan.savings ?? 0).toFixed(2) }}
              </div>

              <div class="plan-description">
                {{ plan.description }}
              </div>

              <ul class="plan-features">
                <li v-for="(feature, index) in normalizeFeatures(plan.features)" :key="index">
                  ✓ {{ feature }}
                </li>
              </ul>
            </div>

            <div class="plan-actions">
              <button
                class="btn btn-primary plan-btn"
                :class="selectedPlan?.id === plan.id ? 'btn--success' : 'btn-ghost'"
                type="button"
                @click.stop="handlePlanButton(plan)"
              >
                {{ selectedPlan?.id === plan.id ? 'Оплатить' : 'Выбрать' }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="selectedPlan" class="plan-continue">
        <div class="plan-summary">
          Выбран план: <strong>{{ selectedPlan.name }}</strong> · К оплате: <strong>${{ Number(selectedPlan.final_price ?? selectedPlan.price).toFixed(2) }}</strong>
        </div>
        <button class="btn btn-primary btn--lg" @click="step = 'payment'">
          Перейти к оплате →
        </button>
      </div>
    </template>

    <!-- Шаг 2: Оплата -->
    <template v-else-if="step === 'payment'">
      <div class="payment-wrap">
        <button class="btn-back" @click="step = 'plans'">← Назад к тарифам</button>

        <div class="payment-layout">
          <div class="payment-form card">
            <h2>💳 Данные карты</h2>
            <p class="form-note">Все данные передаются в зашифрованном виде (AES-256)</p>

            <div class="form-group">
              <label>Номер карты</label>
              <input
                v-model="payForm.card_number"
                type="text"
                class="input card-input"
                :class="{ 'input--error': errors.card_number }"
                placeholder="0000 0000 0000 0000"
                maxlength="19"
                @input="formatCardNumber"
              />
              <p v-if="errors.card_number" class="input-error-msg">{{ errors.card_number[0] }}</p>
              <div class="card-icons">💳 Visa · Mastercard · МИР</div>
            </div>

            <div class="form-group">
              <label>Имя держателя (латиницей)</label>
              <input
                v-model="payForm.card_holder"
                type="text"
                class="input"
                :class="{ 'input--error': errors.card_holder }"
                placeholder="IVAN PETROV"
                @input="payForm.card_holder = payForm.card_holder.toUpperCase()"
              />
              <p v-if="errors.card_holder" class="input-error-msg">{{ errors.card_holder[0] }}</p>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Срок действия</label>
                <input
                  v-model="expireDisplay"
                  type="text"
                  class="input"
                  :class="{ 'input--error': errors.expire_month || errors.expire_year }"
                  placeholder="ММ/ГГ"
                  maxlength="7"
                  @input="formatExpire"
                />
                <p v-if="errors.expire_month || errors.expire_year" class="input-error-msg">
                  {{ (errors.expire_month || errors.expire_year || [])[0] }}
                </p>
              </div>
              <div class="form-group">
                <label>CVV / CVC</label>
                <input
                  v-model="payForm.cvv"
                  type="password"
                  class="input"
                  :class="{ 'input--error': errors.cvv }"
                  placeholder="•••"
                  maxlength="4"
                  @input="payForm.cvv = payForm.cvv.replace(/\D/g, '')"
                />
                <p v-if="errors.cvv" class="input-error-msg">{{ errors.cvv[0] }}</p>
              </div>
            </div>

            <button class="btn btn-primary w-full btn--lg" @click="submitPayment" :disabled="paying">
              <span v-if="paying" class="spinner"></span>
              <span v-else>Оплатить ${{ Number(selectedPlan?.final_price ?? selectedPlan?.price).toFixed(2) }}</span>
            </button>
          </div>

          <div class="order-summary card">
            <h3>Итог заказа</h3>
            <div class="summary-row">
              <span>Тариф</span>
              <span>{{ selectedPlan?.name }}</span>
            </div>
            <div class="summary-row" v-if="selectedPlan?.discount_percent > 0">
              <span>Цена без скидки</span>
              <span class="text-muted">${{ Number(selectedPlan?.price).toFixed(2) }}</span>
            </div>
            <div class="summary-row" v-if="selectedPlan?.discount_percent > 0">
              <span>Скидка</span>
              <span class="text-green">-{{ Number(selectedPlan?.discount_percent).toFixed(0) }}%</span>
            </div>
            <div class="summary-divider"></div>
            <div class="summary-row summary-total">
              <span>К оплате</span>
              <strong>${{ Number(selectedPlan?.final_price ?? selectedPlan?.price).toFixed(2) }}</strong>
            </div>
            <div class="security-note">🔒 Безопасная оплата · SSL</div>
          </div>
        </div>
      </div>
    </template>

    <!-- Шаг 3: Ожидание оплаты -->
    <template v-else-if="step === 'processing'">
      <div class="processing-wrap">
        <div class="processing-icon">⏳</div>
        <h2>Ожидайте оплаты...</h2>
        <p>Обрабатываем ваш платёж. Не закрывайте страницу.</p>
        <div class="processing-bar">
          <div class="processing-fill" :style="{ width: processingPct + '%' }"></div>
        </div>
        <p class="processing-pct">{{ processingPct }}%</p>
      </div>
    </template>

    <!-- Шаг 4: Успех -->
    <template v-else-if="step === 'success'">
      <div class="success-wrap">
        <div class="success-icon">🎉</div>
        <h2>Оплата прошла успешно!</h2>
        <p>Подписка <strong>{{ selectedPlan?.name }}</strong> активирована.</p>

        <div v-if="!cardSaveDecided" class="save-card-prompt card">
          <h3>💾 Сохранить данные карты?</h3>
          <p>Для быстрой оплаты в будущем. Данные хранятся в зашифрованном виде (AES-256).</p>
          <p class="card-preview">{{ maskedCard }}</p>
          <div class="save-card-btns">
            <button class="btn btn-primary" @click="saveCard(true)">Да, сохранить</button>
            <button class="btn btn-ghost" @click="saveCard(false)">Нет, спасибо</button>
          </div>
        </div>

        <div v-else>
          <div v-if="cardSaved" class="alert alert-success">✓ Карта сохранена</div>
          <router-link to="/chat" class="btn btn-primary btn--lg" style="margin-top:16px">
            💬 Перейти в чат →
          </router-link>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import axios from 'axios'

const auth = useAuthStore()

const step = ref('plans')
const plans = ref([])
const selectedPlan = ref(null)
const paying = ref(false)
const processingPct = ref(0)
const errors = ref({})
const expireDisplay = ref('')
const cardSaveDecided = ref(false)
const cardSaved = ref(false)
const maskedCard = ref('')

const payForm = reactive({
  card_number: '',
  card_holder: '',
  expire_month: '',
  expire_year: '',
  cvv: '',
})

onMounted(async () => {
  const { data } = await axios.get('/subscription/plans')
  plans.value = data
})

function selectPlan(plan) {
  selectedPlan.value = plan
}

function handlePlanButton(plan) {
  if (selectedPlan.value?.id === plan.id) {
    step.value = 'payment'
    return
  }

  selectedPlan.value = plan
}

function normalizeFeatures(features) {
  if (Array.isArray(features)) return features
  try {
    return JSON.parse(features || '[]')
  } catch {
    return []
  }
}

function formatCardNumber() {
  const digits = payForm.card_number.replace(/\D/g, '').slice(0, 16)
  payForm.card_number = digits.replace(/(.{4})/g, '$1 ').trim()
}

function formatExpire() {
  const digits = expireDisplay.value.replace(/\D/g, '').slice(0, 6)
  if (digits.length >= 3) {
    expireDisplay.value = digits.slice(0, 2) + '/' + digits.slice(2)
    payForm.expire_month = digits.slice(0, 2)
    const yearPart = digits.slice(2)
    payForm.expire_year = yearPart.length <= 2 ? '20' + yearPart.padEnd(2, '0') : yearPart
  } else {
    expireDisplay.value = digits
    payForm.expire_month = digits.slice(0, 2)
    payForm.expire_year = ''
  }
}

function validatePayForm() {
  const errs = {}
  const digits = payForm.card_number.replace(/\s/g, '')

  if (!digits || !/^\d{16}$/.test(digits)) {
    errs.card_number = ['Номер карты должен содержать 16 цифр.']
  }

  if (!payForm.card_holder || payForm.card_holder.trim().length < 3) {
    errs.card_holder = ['Введите имя держателя карты (латиницей).']
  } else if (!/^[A-Za-z\s]+$/.test(payForm.card_holder)) {
    errs.card_holder = ['Только латинские буквы.']
  }

  if (!payForm.expire_month || !payForm.expire_year) {
    errs.expire_month = ['Введите срок действия карты в формате ММ/ГГ.']
  } else {
    const now = new Date()
    const y = parseInt(payForm.expire_year)
    const m = parseInt(payForm.expire_month)
    if (y < now.getFullYear() || (y === now.getFullYear() && m < now.getMonth() + 1)) {
      errs.expire_month = ['Срок действия карты истёк.']
    }
  }

  if (!payForm.cvv || !/^\d{3,4}$/.test(payForm.cvv)) {
    errs.cvv = ['CVV должен содержать 3 или 4 цифры.']
  }

  return errs
}

async function submitPayment() {
  const clientErrors = validatePayForm()
  if (Object.keys(clientErrors).length > 0) {
    errors.value = clientErrors
    return
  }

  errors.value = {}
  paying.value = true
  step.value = 'processing'

  processingPct.value = 0
  const interval = setInterval(() => {
    processingPct.value = Math.min(processingPct.value + 2, 95)
  }, 100)

  try {
    await axios.post('/subscription/purchase', {
      plan_id: selectedPlan.value.id,
      card_number: payForm.card_number.replace(/\s/g, ''),
      card_holder: payForm.card_holder,
      expire_month: payForm.expire_month,
      expire_year: payForm.expire_year,
      cvv: payForm.cvv,
      save_card: false,
    })

    await new Promise(r => setTimeout(r, 500))
    clearInterval(interval)
    processingPct.value = 100
    await new Promise(r => setTimeout(r, 400))

    await auth.fetchMe()

    maskedCard.value = '**** **** **** ' + payForm.card_number.replace(/\s/g, '').slice(-4)
    step.value = 'success'
  } catch (e) {
    clearInterval(interval)
    step.value = 'payment'
    paying.value = false

    if (e.response?.data?.errors) {
      errors.value = e.response.data.errors
    } else if (e.response?.data?.message) {
      errors.value = { card_number: [e.response.data.message] }
    } else {
      errors.value = { card_number: ['Ошибка оплаты. Проверьте данные и попробуйте снова.'] }
    }
  }
}

async function saveCard(save) {
  if (save) {
    try {
      await axios.post('/subscription/purchase', {
        plan_id: selectedPlan.value.id,
        card_number: payForm.card_number.replace(/\s/g, ''),
        card_holder: payForm.card_holder,
        expire_month: payForm.expire_month,
        expire_year: payForm.expire_year,
        cvv: payForm.cvv,
        save_card: true,
      })
      cardSaved.value = true
    } catch {}
  }
  cardSaveDecided.value = true
}
</script>

<style scoped>
.plans-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
  margin-bottom: 28px;
}

.plan-card {
  background: var(--bg-card);
  border: 2px solid var(--border);
  border-radius: var(--radius);
  padding: 24px 20px;
  cursor: pointer;
  position: relative;
  transition: all .2s;
  height: 100%;
}

.plan-card:hover {
  border-color: var(--accent);
  transform: translateY(-2px);
}

.plan-card--selected {
  border-color: var(--accent);
  box-shadow: 0 0 0 4px var(--accent-glow);
}

.plan-card--popular {
  border-color: var(--green);
}

.plan-badge {
  position: absolute;
  top: -11px;
  left: 50%;
  transform: translateX(-50%);
  background: var(--green);
  color: #000;
  font-size: 11px;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 20px;
  white-space: nowrap;
}

.plan-badge--blue {
  background: var(--accent);
  color: #fff;
}

.plan-content {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.plan-top {
  display: flex;
  flex-direction: column;
}

.plan-name {
  font-size: 1.1rem;
  font-weight: 700;
  margin-bottom: 10px;
}

.plan-price-wrap {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 4px;
}

.plan-price-old {
  color: var(--text-muted);
  text-decoration: line-through;
  font-size: 14px;
}

.plan-price-final {
  font-size: 1.8rem;
  font-weight: 800;
  color: var(--green);
}

.plan-discount {
  font-size: 12px;
  color: var(--green);
  margin-bottom: 14px;
}

.plan-description {
  font-size: 13px;
  line-height: 1.5;
  color: var(--text-muted);
  margin-bottom: 14px;
  min-height: 54px;
}

.plan-features {
  list-style: none;
  margin: 0;
  padding: 0;
}

.plan-features li {
  font-size: 13px;
  color: var(--text-muted);
  padding: 4px 0;
}

.plan-actions {
  margin-top: auto;
  padding-top: 18px;
  display: flex;
  justify-content: center;
}

.plan-btn {
  width: 100%;
  max-width: 220px;
}

.btn--success {
  background: #16a34a;
  border-color: #16a34a;
  color: #fff;
}

.btn--success:hover {
  background: #15803d;
  border-color: #15803d;
}

.plan-continue {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 16px 24px;
  margin-bottom: 8px;
}

.plan-summary {
  font-size: 15px;
  color: var(--text-muted);
}

.payment-wrap {
  max-width: 860px;
}

.btn-back {
  background: none;
  border: none;
  color: var(--accent);
  cursor: pointer;
  font-size: 14px;
  margin-bottom: 20px;
}

.payment-layout {
  display: grid;
  grid-template-columns: 1fr 320px;
  gap: 20px;
}

.payment-form h2 {
  margin-bottom: 4px;
}

.form-note {
  font-size: 12px;
  color: var(--text-muted);
  margin-bottom: 20px;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  font-size: 13px;
  font-weight: 600;
  color: var(--text-muted);
  margin-bottom: 6px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.card-icons {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 6px;
}

.w-full {
  width: 100%;
}

.order-summary h3 {
  margin-bottom: 16px;
}

.summary-row {
  display: flex;
  justify-content: space-between;
  font-size: 14px;
  padding: 6px 0;
}

.summary-divider {
  border-top: 1px solid var(--border);
  margin: 10px 0;
}

.summary-total {
  font-size: 1.05rem;
}

.text-green {
  color: var(--green);
}

.text-muted {
  color: var(--text-muted);
}

.security-note {
  font-size: 12px;
  color: var(--text-muted);
  text-align: center;
  margin-top: 16px;
}

.processing-wrap, .success-wrap {
  max-width: 500px;
  margin: 60px auto;
  text-align: center;
}

.processing-icon, .success-icon {
  font-size: 4rem;
  margin-bottom: 16px;
}

.success-icon {
  animation: bounce .6s;
}

@keyframes bounce {
  0%,100% { transform: scale(1); }
  50% { transform: scale(1.2); }
}

.processing-wrap h2, .success-wrap h2 {
  font-size: 1.5rem;
  margin-bottom: 8px;
}

.processing-wrap p, .success-wrap p {
  color: var(--text-muted);
}

.processing-bar {
  height: 6px;
  background: var(--bg-card);
  border-radius: 3px;
  margin: 20px 0 8px;
  overflow: hidden;
}

.processing-fill {
  height: 100%;
  background: var(--accent);
  transition: width .1s;
  border-radius: 3px;
}

.processing-pct {
  font-size: 13px;
  color: var(--text-muted);
}

.save-card-prompt {
  max-width: 440px;
  margin: 20px auto;
  text-align: left;
}

.save-card-prompt h3 {
  margin-bottom: 8px;
}

.save-card-prompt p {
  color: var(--text-muted);
  font-size: 14px;
  margin-bottom: 4px;
}

.card-preview {
  font-family: monospace;
  font-size: 1rem;
  color: var(--text);
  margin: 10px 0;
}

.save-card-btns {
  display: flex;
  gap: 10px;
  margin-top: 16px;
}

@media (max-width: 700px) {
  .payment-layout {
    grid-template-columns: 1fr;
  }

  .plan-continue {
    flex-direction: column;
    gap: 12px;
    align-items: stretch;
  }
}
</style>