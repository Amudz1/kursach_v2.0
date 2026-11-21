<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { explain } from '../api'
import { useSession } from '../session'
import UiButton from '../components/ui/UiButton.vue'
import UiInput from '../components/ui/UiInput.vue'
import UiProgress from '../components/ui/UiProgress.vue'
import UiSegmented from '../components/ui/UiSegmented.vue'

const session = useSession()
const router = useRouter()
const mode = ref<'login' | 'register'>('login')
const registering = computed(() => mode.value === 'register')
const authModes = [{ value: 'login', label: 'Вход' }, { value: 'register', label: 'Регистрация' }] as const
const login = ref('')
const email = ref('')
const password = ref('')
const confirmation = ref('')
const error = ref('')
const busy = ref(false)
const strength = computed(() => {
  const value = password.value
  if (value.length < 8) return value.length ? 1 : 0
  return 2 + Number(value.length >= 12) + Number(/\p{L}/u.test(value) && /[\d\p{P}\p{S}]/u.test(value))
})

async function submit() {
  error.value = ''
  busy.value = true
  try {
    if (registering.value) await session.signIn('register', {
      username: login.value, email: email.value, password: password.value, passwordConfirmation: confirmation.value,
    })
    else await session.signIn('login', { login: login.value, password: password.value })
    await router.replace('/chats')
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
</script>

<template>
  <div class="auth-shell">
    <div class="auth-side">
      <div class="brand auth-brand"><span class="brand-mark" aria-hidden="true">b<span>.</span></span><span>budz<span class="auth-brand-dot">.</span></span></div>
      <div class="auth-heading">
        <h1>Ассистент с&nbsp;настройкой под&nbsp;вас</h1>
        <p>Выберите роль ассистента и задайте, как он отвечает. Вы управляете тем, что Budz запоминает.</p>
      </div>
    </div>
    <div class="auth-form">
      <UiSegmented v-model="mode" label="Вход или регистрация" :options="authModes" hide-label block @update:model-value="error = ''" />
      <h2>{{ registering ? 'Создать аккаунт' : 'Войти в Budz' }}</h2>
      <form class="stack" @submit.prevent="submit">
        <UiInput v-model="login" :label="registering ? 'Имя пользователя' : 'Логин или email'" autocomplete="username" :maxlength="registering ? 30 : 254" required />
        <UiInput v-if="registering" v-model="email" label="Email" type="email" autocomplete="email" :maxlength="254" required />
        <UiInput v-model="password" label="Пароль" type="password" reveal :autocomplete="registering ? 'new-password' : 'current-password'"
          :minlength="registering ? 8 : undefined" :maxlength="128" required />
        <div v-if="registering && password" class="strength">
          <UiProgress :value="strength" :max="4" label="Надёжность пароля" meter />
          <span>Надёжность: {{ strength <= 1 ? 'низкая' : strength <= 3 ? 'средняя' : 'высокая' }}</span>
        </div>
        <UiInput v-if="registering" v-model="confirmation" label="Повторите пароль" type="password" reveal
          autocomplete="new-password" minlength="8" :maxlength="128" required />
        <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
        <UiButton variant="primary" :loading="busy" type="submit">{{ registering ? 'Создать аккаунт' : 'Войти' }}</UiButton>
      </form>
    </div>
  </div>
</template>

<style scoped>
.auth-side { position: relative; isolation: isolate; overflow: hidden; }
.auth-side::before { content: ''; position: absolute; inset: -22%; z-index: -1; opacity: .8;
  background-image: radial-gradient(ellipse at 25% 20%, var(--auth-wash-secondary, color-mix(in oklab, var(--accent-2) 65%, transparent)), transparent 55%),
    radial-gradient(ellipse at 75% 75%, var(--auth-wash-primary, color-mix(in oklab, var(--accent) 55%, transparent)), transparent 58%);
  animation: drift 16s var(--ease-out) alternate infinite; }
@keyframes drift { from { transform: scale(1) translate(-2%, -3%); } to { transform: scale(1.18) translate(5%, 4%); } }
.auth-form { align-content: start; }
.auth-brand-dot { color: var(--studio-bookmark); }
.strength { display: grid; gap: var(--space-1); font-size: var(--font-size-xs); color: var(--text-muted); }
</style>
