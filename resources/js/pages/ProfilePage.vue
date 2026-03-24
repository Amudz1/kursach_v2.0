<template>
  <div class="page">
    <div class="page-title">👤 Профиль</div>

    <div v-if="loading" class="loading-wrap">
      <span class="spinner"></span> Загрузка...
    </div>

    <div v-else class="profile-layout">

      <!-- Левая колонка: данные пользователя -->
      <div class="profile-left">

        <!-- Аватар + имя -->
        <div class="card profile-header">
          <div class="profile-avatar">{{ profile?.user?.username?.[0]?.toUpperCase() }}</div>
          <div>
            <h2 class="profile-name">{{ profile?.user?.username }}</h2>
            <p class="profile-email">{{ profile?.user?.email || 'Email не привязан' }}</p>
          </div>
        </div>

        <!-- Статус подписки -->
        <div class="card sub-info-card" :class="profile?.subscription?.has_subscription ? 'sub-active' : 'sub-none'">
          <div class="sub-info-header">
            <span class="sub-icon">{{ profile?.subscription?.has_subscription ? '⭐' : '🆓' }}</span>
            <div>
              <div class="sub-info-title">
                {{ profile?.subscription?.has_subscription ? 'PRO-подписка' : 'Бесплатный план' }}
              </div>
              <div class="sub-info-plan">{{ profile?.subscription?.status }}</div>
            </div>
          </div>

          <template v-if="profile?.subscription?.has_subscription">
            <div class="sub-detail-row">
              <span>Тариф</span>
              <strong>{{ profile.subscription.plan_name }}</strong>
            </div>
            <div class="sub-detail-row">
              <span>Действует до</span>
              <strong>{{ profile.subscription.ends_at }}</strong>
            </div>
            <div class="sub-detail-row">
              <span>Осталось дней</span>
              <strong :class="profile.subscription.days_remaining < 7 ? 'text-red' : 'text-green'">
                {{ profile.subscription.days_remaining }} дн.
              </strong>
            </div>
            <div class="sub-detail-row">
              <span>Запросов</span>
              <strong class="text-green">Безлимит</strong>
            </div>
          </template>
          <template v-else>
            <div class="sub-detail-row">
              <span>Осталось запросов</span>
              <strong>{{ profile?.remaining_prompts }} / 12</strong>
            </div>
          </template>

          <router-link
            :to="'/subscription'"
            class="btn btn-primary"
            style="width:100%;margin-top:14px;justify-content:center"
          >
            {{ profile?.subscription?.has_subscription ? '🔄 Продлить подписку' : '✨ Оформить подписку' }}
          </router-link>
        </div>

        <!-- Сохранённые карты -->
        <div class="card" v-if="profile?.payment_methods?.length">
          <h3 class="section-label">💳 Сохранённые карты</h3>
          <div v-for="pm in profile.payment_methods" :key="pm.id" class="payment-method-row">
            <div>
              <div class="card-number">{{ pm.masked_number }}</div>
              <div class="card-meta">{{ pm.card_holder }} · {{ pm.expire }}</div>
            </div>
            <span v-if="pm.is_default" class="default-badge">По умолчанию</span>
          </div>
        </div>
      </div>

      <!-- Правая колонка: настройки -->
      <div class="profile-right">

        <!-- Изменить email -->
        <div class="card settings-card">
          <h3 class="section-label">📧 Изменить email</h3>
          <div v-if="emailAlert.text" :class="['alert', `alert-${emailAlert.type}`]">{{ emailAlert.text }}</div>
          <div class="form-group">
            <label>Новый email</label>
            <input v-model="emailForm.email" type="email" class="input" placeholder="your@email.com"/>
          </div>
          <div class="form-group">
            <label>Подтверждение пароля</label>
            <input v-model="emailForm.password" type="password" class="input" placeholder="Текущий пароль"/>
          </div>
          <button class="btn btn-primary" @click="updateEmail" :disabled="emailLoading">
            <span v-if="emailLoading" class="spinner"></span>
            <span v-else>Сохранить</span>
          </button>
        </div>

        <!-- Изменить пароль -->
        <div class="card settings-card">
          <h3 class="section-label">🔒 Изменить пароль</h3>
          <div v-if="passAlert.text" :class="['alert', `alert-${passAlert.type}`]">{{ passAlert.text }}</div>
          <div class="form-group">
            <label>Текущий пароль</label>
            <input v-model="passForm.current_password" type="password" class="input"/>
          </div>
          <div class="form-group">
            <label>Новый пароль</label>
            <input v-model="passForm.password" type="password" class="input" placeholder="Минимум 6 символов"/>
          </div>
          <div class="form-group">
            <label>Повтор пароля</label>
            <input v-model="passForm.password_confirmation" type="password" class="input"/>
          </div>
          <button class="btn btn-primary" @click="updatePassword" :disabled="passLoading">
            <span v-if="passLoading" class="spinner"></span>
            <span v-else>Изменить пароль</span>
          </button>
        </div>

        <!-- Опасная зона -->
        <div class="card danger-zone">
          <h3 class="section-label danger-label">⚠️ Опасная зона</h3>

          <!-- Выход -->
          <div class="danger-row">
            <div>
              <div class="danger-title">Выйти из аккаунта</div>
              <div class="danger-desc">Завершить текущую сессию</div>
            </div>
            <button class="btn btn-ghost" @click="confirmLogout">Выйти</button>
          </div>

          <!-- Удаление аккаунта -->
          <div class="danger-row">
            <div>
              <div class="danger-title">Удалить аккаунт</div>
              <div class="danger-desc">Все данные будут удалены безвозвратно</div>
            </div>
            <button class="btn btn-danger" @click="showDeleteModal = true">Удалить</button>
          </div>
        </div>
      </div>
    </div>

    <!-- Модалка: подтверждение выхода -->
    <div v-if="showLogoutModal" class="modal-overlay" @click.self="showLogoutModal = false">
      <div class="modal-box">
        <h3>Выйти из аккаунта?</h3>
        <p>Вы уверены, что хотите завершить сессию?</p>
        <div class="modal-actions">
          <button class="btn btn-primary" @click="doLogout">Да, выйти</button>
          <button class="btn btn-ghost"   @click="showLogoutModal = false">Отмена</button>
        </div>
      </div>
    </div>

    <!-- Модалка: подтверждение удаления -->
    <div v-if="showDeleteModal" class="modal-overlay" @click.self="showDeleteModal = false">
      <div class="modal-box">
        <h3>🗑 Удалить аккаунт?</h3>
        <p>Это действие <strong>нельзя отменить</strong>. Все ваши чаты, данные и подписка будут удалены.</p>
        <div v-if="deleteAlert.text" :class="['alert', `alert-${deleteAlert.type}`]">{{ deleteAlert.text }}</div>
        <div class="form-group" style="margin-top:16px">
          <label>Введите пароль для подтверждения</label>
          <input v-model="deletePassword" type="password" class="input" placeholder="Ваш пароль"/>
        </div>
        <div class="modal-actions">
          <button class="btn btn-danger" @click="deleteAccount" :disabled="deleteLoading">
            <span v-if="deleteLoading" class="spinner"></span>
            <span v-else>Удалить навсегда</span>
          </button>
          <button class="btn btn-ghost" @click="showDeleteModal = false">Отмена</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import axios from 'axios'

const router = useRouter()
const auth   = useAuthStore()

const loading  = ref(true)
const profile  = ref(null)

const emailForm    = reactive({ email: '', password: '' })
const emailLoading = ref(false)
const emailAlert   = reactive({ type: '', text: '' })

const passForm    = reactive({ current_password: '', password: '', password_confirmation: '' })
const passLoading = ref(false)
const passAlert   = reactive({ type: '', text: '' })

const showLogoutModal = ref(false)
const showDeleteModal = ref(false)
const deletePassword  = ref('')
const deleteLoading   = ref(false)
const deleteAlert     = reactive({ type: '', text: '' })

onMounted(async () => {
  const { data } = await axios.get('/profile')
  profile.value  = data
  emailForm.email = data.user?.email || ''
  loading.value  = false
})

async function updateEmail() {
  emailLoading.value = true
  emailAlert.text = ''
  try {
    await axios.put('/profile/email', emailForm)
    emailAlert.type = 'success'
    emailAlert.text = 'Email успешно обновлён!'
    emailForm.password = ''
    await auth.fetchMe()
  } catch (e) {
    emailAlert.type = 'error'
    emailAlert.text = e.response?.data?.message || 'Ошибка'
  } finally {
    emailLoading.value = false
  }
}

async function updatePassword() {
  passLoading.value = true
  passAlert.text = ''
  try {
    await axios.put('/profile/password', passForm)
    passAlert.type = 'success'
    passAlert.text = 'Пароль успешно изменён!'
    passForm.current_password = ''
    passForm.password = ''
    passForm.password_confirmation = ''
  } catch (e) {
    passAlert.type = 'error'
    passAlert.text = e.response?.data?.message || 'Ошибка'
  } finally {
    passLoading.value = false
  }
}

function confirmLogout() { showLogoutModal.value = true }

async function doLogout() {
  await auth.logout()
  router.push('/login')
}

async function deleteAccount() {
  deleteLoading.value = true
  deleteAlert.text = ''
  try {
    await axios.delete('/account', { data: { password: deletePassword.value } })
    await auth.logout()
    router.push('/login')
  } catch (e) {
    deleteAlert.type = 'error'
    deleteAlert.text = e.response?.data?.message || 'Ошибка удаления'
    deleteLoading.value = false
  }
}
</script>

<style scoped>
.loading-wrap { display: flex; align-items: center; gap: 12px; padding: 40px; color: var(--text-muted); }

.profile-layout {
  display: grid;
  grid-template-columns: 340px 1fr;
  gap: 20px;
  align-items: start;
}

/* Left */
.profile-header { display: flex; align-items: center; gap: 16px; margin-bottom: 0; }
.profile-avatar {
  width: 56px; height: 56px;
  background: var(--accent);
  border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: 1.5rem; font-weight: 800; color: #fff;
  flex-shrink: 0;
}
.profile-name  { font-size: 1.2rem; font-weight: 700; }
.profile-email { font-size: 13px; color: var(--text-muted); }

.sub-info-card { }
.sub-active { border-color: rgba(74,222,128,.4); }
.sub-none   { border-color: rgba(251,146,60,.3); }
.sub-info-header { display: flex; align-items: center; gap: 12px; margin-bottom: 16px; }
.sub-icon   { font-size: 1.6rem; }
.sub-info-title { font-weight: 700; }
.sub-info-plan  { font-size: 13px; color: var(--text-muted); }
.sub-detail-row { display: flex; justify-content: space-between; font-size: 14px; padding: 7px 0; border-bottom: 1px solid var(--border); }
.sub-detail-row:last-of-type { border-bottom: none; }
.text-green { color: var(--green); }
.text-red   { color: var(--red); }

.section-label { font-size: 14px; font-weight: 700; color: var(--text-muted); margin-bottom: 14px; text-transform: uppercase; letter-spacing: .05em; }

.payment-method-row { display: flex; justify-content: space-between; align-items: center; padding: 10px 0; border-bottom: 1px solid var(--border); }
.payment-method-row:last-child { border-bottom: none; }
.card-number { font-family: monospace; font-size: 15px; }
.card-meta   { font-size: 12px; color: var(--text-muted); margin-top: 2px; }
.default-badge { font-size: 11px; background: var(--accent-glow); color: var(--accent); padding: 3px 8px; border-radius: 12px; }

/* Right */
.profile-left, .profile-right { display: flex; flex-direction: column; gap: 16px; }
.settings-card .form-group { margin-bottom: 12px; }
.settings-card .form-group label { display: block; font-size: 13px; color: var(--text-muted); margin-bottom: 5px; }

/* Danger */
.danger-zone { border-color: rgba(248,113,113,.3); }
.danger-label { color: var(--red); }
.danger-row { display: flex; justify-content: space-between; align-items: center; padding: 14px 0; border-bottom: 1px solid var(--border); }
.danger-row:last-child { border-bottom: none; }
.danger-title { font-weight: 600; font-size: 14px; }
.danger-desc  { font-size: 12px; color: var(--text-muted); margin-top: 2px; }

/* Modal */
.modal-overlay {
  position: fixed; inset: 0;
  background: rgba(0,0,0,.7);
  display: flex; align-items: center; justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}
.modal-box {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 28px 32px;
  max-width: 440px; width: 100%;
  box-shadow: var(--shadow);
}
.modal-box h3 { font-size: 1.2rem; margin-bottom: 10px; }
.modal-box p  { color: var(--text-muted); font-size: 14px; }
.modal-actions { display: flex; gap: 10px; margin-top: 20px; }

@media (max-width: 900px) {
  .profile-layout { grid-template-columns: 1fr; }
}
</style>
