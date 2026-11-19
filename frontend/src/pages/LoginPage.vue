<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { explain } from '../api'
import { useSession } from '../session'

const session = useSession()
const router = useRouter()
const registering = ref(false)
const login = ref('')
const email = ref('')
const password = ref('')
const confirmation = ref('')
const error = ref('')
const busy = ref(false)

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
      <div class="brand auth-brand"><span class="brand-mark" aria-hidden="true">b<span>.</span></span>budz.</div>
      <div class="auth-heading">
        <h1>Ассистент с&nbsp;настройкой под&nbsp;вас</h1>
        <p>Выберите роль ассистента и задайте, как он отвечает. Вы управляете тем, что Budz запоминает.</p>
      </div>
      <div class="auth-decoration" aria-hidden="true"><i></i><i></i><i></i></div>
    </div>
    <div class="auth-form">
      <h2>{{ registering ? 'Создать аккаунт' : 'Войти в Budz' }}</h2>
      <form class="stack" @submit.prevent="submit">
        <label>{{ registering ? 'Имя пользователя' : 'Логин или email' }}
          <input v-model="login" autocomplete="username" :maxlength="registering ? 30 : 254" required>
        </label>
        <label v-if="registering">Email
          <input v-model="email" type="email" autocomplete="email" maxlength="254" required>
        </label>
        <label>Пароль
          <input v-model="password" type="password" :autocomplete="registering ? 'new-password' : 'current-password'"
            :minlength="registering ? 8 : undefined" maxlength="128" required>
        </label>
        <label v-if="registering">Повторите пароль
          <input v-model="confirmation" type="password" autocomplete="new-password" minlength="8" maxlength="128" required>
        </label>
        <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
        <button class="primary" :disabled="busy" type="submit">{{ busy ? 'Подождите…' : registering ? 'Создать аккаунт' : 'Войти' }}</button>
        <button type="button" class="plain-button" @click="registering = !registering; error = ''">
          {{ registering ? 'Уже есть аккаунт' : 'Создать аккаунт' }}
        </button>
      </form>
    </div>
  </div>
</template>
