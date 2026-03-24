<template>
  <div class="login-wrap">
    <div class="login-card">
      <!-- Лого -->
      <div class="login-logo">🤖</div>
      <h1 class="login-title">AI Helper Budz</h1>
      <p class="login-subtitle">Ваш персональный AI-ассистент</p>

      <!-- Табы -->
      <div class="tabs">
        <button :class="['tab', { active: mode === 'login' }]"    @click="mode = 'login'">Вход</button>
        <button :class="['tab', { active: mode === 'register' }]" @click="mode = 'register'">Регистрация</button>
      </div>

      <!-- Сообщение -->
      <div v-if="alert.text" :class="['alert', `alert-${alert.type}`]">{{ alert.text }}</div>

      <!-- Форма входа -->
      <form v-if="mode === 'login'" @submit.prevent="handleLogin" class="form">
        <div class="form-group">
          <label>Логин</label>
          <input v-model="form.username" type="text" class="input" :class="{ 'input--error': errors.username }"
                 placeholder="Введите логин" autocomplete="username" required/>
          <p v-if="errors.username" class="input-error-msg">{{ errors.username[0] }}</p>
        </div>
        <div class="form-group">
          <label>Пароль</label>
          <div class="input-wrap">
            <input v-model="form.password" :type="showPass ? 'text' : 'password'"
                   class="input" placeholder="Введите пароль" autocomplete="current-password" required/>
            <button type="button" class="pass-toggle" @click="showPass = !showPass">
              {{ showPass ? '🙈' : '👁' }}
            </button>
          </div>
          <p v-if="errors.password" class="input-error-msg">{{ errors.password[0] }}</p>
        </div>
        <button type="submit" class="btn btn-primary w-full" :disabled="loading">
          <span v-if="loading" class="spinner"></span>
          <span v-else>Войти</span>
        </button>
      </form>

      <!-- Форма регистрации -->
      <form v-else @submit.prevent="handleRegister" class="form">
        <div class="form-group">
          <label>Логин <span class="req">*</span></label>
          <input v-model="form.username" type="text" class="input" :class="{ 'input--error': errors.username }"
                 placeholder="Только буквы, цифры, - _" required/>
          <p v-if="errors.username" class="input-error-msg">{{ errors.username[0] }}</p>
        </div>
        <div class="form-group">
          <label>Email <span class="optional">(необязательно)</span></label>
          <input v-model="form.email" type="email" class="input" :class="{ 'input--error': errors.email }"
                 placeholder="your@email.com"/>
          <p v-if="errors.email" class="input-error-msg">{{ errors.email[0] }}</p>
        </div>
        <div class="form-group">
          <label>Пароль <span class="req">*</span></label>
          <div class="input-wrap">
            <input v-model="form.password" :type="showPass ? 'text' : 'password'"
                   class="input" placeholder="Минимум 6 символов" required/>
            <button type="button" class="pass-toggle" @click="showPass = !showPass">
              {{ showPass ? '🙈' : '👁' }}
            </button>
          </div>
          <p v-if="errors.password" class="input-error-msg">{{ errors.password[0] }}</p>
        </div>
        <div class="form-group">
          <label>Подтверждение пароля <span class="req">*</span></label>
          <input v-model="form.password_confirmation" :type="showPass ? 'text' : 'password'"
                 class="input" placeholder="Повторите пароль" required/>
        </div>
        <button type="submit" class="btn btn-primary w-full" :disabled="loading">
          <span v-if="loading" class="spinner"></span>
          <span v-else>Создать аккаунт</span>
        </button>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import axios from 'axios'

const router = useRouter()
const auth   = useAuthStore()

const mode     = ref('login')
const loading  = ref(false)
const showPass = ref(false)
const errors   = ref({})
const alert    = reactive({ type: '', text: '' })

const form = reactive({
  username: '', email: '', password: '', password_confirmation: ''
})

function resetForm() {
  errors.value = {}
  alert.text = ''
  form.email = ''
  form.password = ''
  form.password_confirmation = ''
}

async function handleLogin() {
  errors.value = {}
  loading.value = true
  try {
    const { data } = await axios.post('/login', {
      username: form.username,
      password: form.password,
    })
    auth.setAuth(data)
    router.push('/')
  } catch (e) {
    if (e.response?.status === 401) {
      alert.type = 'error'
      alert.text = e.response.data.message
    } else if (e.response?.data?.errors) {
      errors.value = e.response.data.errors
    }
  } finally {
    loading.value = false
  }
}

async function handleRegister() {
  errors.value = {}
  loading.value = true
  try {
    const { data } = await axios.post('/register', form)
    auth.setAuth(data)
    router.push('/')
  } catch (e) {
    if (e.response?.data?.errors) {
      errors.value = e.response.data.errors
    }
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-wrap {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: radial-gradient(ellipse at 50% 0%, rgba(108,99,255,.15) 0%, var(--bg) 70%);
}

.login-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 20px;
  padding: 44px 40px;
  width: 100%;
  max-width: 440px;
  box-shadow: var(--shadow);
}

.login-logo    { font-size: 3.5rem; text-align: center; margin-bottom: 8px; }
.login-title   { font-size: 1.7rem; font-weight: 800; text-align: center; color: var(--accent); }
.login-subtitle{ color: var(--text-muted); text-align: center; margin-bottom: 28px; font-size: 14px; }

.tabs { display: flex; gap: 4px; background: var(--bg); border-radius: var(--radius-sm); padding: 4px; margin-bottom: 24px; }
.tab  { flex: 1; padding: 9px; border: none; border-radius: var(--radius-sm); background: transparent; color: var(--text-muted); font-size: 14px; font-weight: 600; cursor: pointer; transition: all .2s; }
.tab.active { background: var(--bg-card); color: var(--text); box-shadow: 0 2px 8px rgba(0,0,0,.3); }

.form { display: flex; flex-direction: column; gap: 16px; }
.form-group label { display: block; font-size: 13px; font-weight: 600; color: var(--text-muted); margin-bottom: 6px; }
.req      { color: var(--red); }
.optional { color: var(--text-muted); font-weight: 400; font-size: 11px; }

.input-wrap { position: relative; }
.input-wrap .input { padding-right: 42px; }
.pass-toggle { position: absolute; right: 12px; top: 50%; transform: translateY(-50%); background: none; border: none; cursor: pointer; font-size: 16px; }

.w-full { width: 100%; }
</style>
