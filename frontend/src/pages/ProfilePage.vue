<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { api, explain } from '../api'
import { useSession } from '../session'
import StatisticsPanel from '../components/StatisticsPanel.vue'
import ThemePicker from '../components/ThemePicker.vue'
import type { Statistics } from '../types'

const session = useSession()
const router = useRouter()
const email = reactive({ email: session.account?.email ?? '', password: '' })
const passwords = reactive({ currentPassword: '', password: '', passwordConfirmation: '' })
const dialog = ref<HTMLDialogElement>()
const removePassword = ref('')
const error = ref('')
const success = ref('')
const busy = ref(false)
const deletionError = ref('')
const statistics = ref<Statistics>()
const days = ref(30)
const statsError = ref('')
const statsLoading = ref(false)
async function loadStatistics() {
  statsError.value = ''; statsLoading.value = true
  try { statistics.value = await api<Statistics>(`/me/stats?days=${days.value}`) }
  catch (reason) { statsError.value = explain(reason) }
  finally { statsLoading.value = false }
}
onMounted(loadStatistics)
async function updateEmail() {
  error.value = ''; success.value = ''; busy.value = true
  try { await api('/me/email', { method: 'PUT', body: email }); email.password = ''; await session.reload(); success.value = 'Email обновлён.' }
  catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
async function updatePassword() {
  error.value = ''; success.value = ''; busy.value = true
  try { await api('/me/password', { method: 'PUT', body: passwords }); session.clear(); await router.replace('/login') }
  catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
async function logout() {
  error.value = ''
  try { await session.signOut(); await router.replace('/login') }
  catch (reason) { error.value = explain(reason) }
}
async function remove() {
  deletionError.value = ''; busy.value = true
  try {
    await api('/me', { method: 'DELETE', body: { password: removePassword.value } })
    dialog.value?.close(); session.clear(); await router.replace('/login')
  } catch (reason) { deletionError.value = explain(reason) }
  finally { busy.value = false }
}
</script>

<template>
  <section class="page">
    <header class="page-heading"><h1>Профиль</h1><p>{{ session.account?.username }} · {{ session.account?.email }}</p></header>
    <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
    <p v-if="success" class="notice success" role="status">{{ success }}</p>
    <div class="profile-grid">
      <div class="stack">
        <ThemePicker />
        <form class="fieldset stack" @submit.prevent="updateEmail">
          <h2>Электронная почта</h2>
          <label>Новый email <input v-model="email.email" type="email" autocomplete="email" maxlength="254" required></label>
          <label>Текущий пароль <input v-model="email.password" type="password" autocomplete="current-password" required></label>
          <button class="primary self-start" type="submit" :disabled="busy">Обновить email</button>
        </form>
        <form class="fieldset stack" @submit.prevent="updatePassword">
          <h2>Пароль</h2>
          <label>Текущий пароль <input v-model="passwords.currentPassword" type="password" autocomplete="current-password" required></label>
          <label>Новый пароль <input v-model="passwords.password" type="password" minlength="8" maxlength="128" autocomplete="new-password" required></label>
          <label>Повторите новый пароль <input v-model="passwords.passwordConfirmation" type="password" minlength="8" maxlength="128" autocomplete="new-password" required></label>
          <p class="help">После смены пароля нужно войти заново.</p>
          <button class="primary self-start" type="submit" :disabled="busy">Сменить пароль</button>
        </form>
      </div>
      <aside class="preview-panel stack">
        <h2>Аккаунт</h2>
        <dl v-if="session.account" class="context-list">
          <div><dt>Тариф</dt><dd>{{ session.account.usage.planName }}</dd></div>
          <div><dt>Токенов использовано</dt><dd>{{ session.account.usage.usedTokensToday.toLocaleString('ru-RU') }}</dd></div>
          <div><dt>Токенов доступно</dt><dd>{{ session.account.usage.remainingTokens?.toLocaleString('ru-RU') ?? 'Безлимит' }}</dd></div>
        </dl>
        <RouterLink class="text-button" to="/plans">Сравнить тарифы ↗</RouterLink>
        <RouterLink class="text-button" to="/data">Управление данными ↗</RouterLink>
        <button class="secondary" @click="logout">Выйти</button>
        <button class="text-button danger-text" @click="removePassword = ''; deletionError = ''; dialog?.showModal()">Удалить аккаунт</button>
      </aside>
    </div>
    <section class="profile-stats stack" aria-labelledby="personal-stats">
      <h2 id="personal-stats">Личная статистика</h2>
      <label class="period">Период <select v-model.number="days" aria-label="Период" @change="loadStatistics">
        <option :value="7">7 дней</option><option :value="30">30 дней</option><option :value="90">90 дней</option>
      </select></label>
      <p v-if="statsLoading" class="muted" role="status">Загружаем статистику…</p>
      <p v-if="statsError" class="notice danger" role="alert">{{ statsError }} <button class="text-button" @click="loadStatistics">Повторить</button></p>
      <StatisticsPanel v-if="statistics" :data="statistics" />
    </section>
    <dialog ref="dialog" aria-labelledby="delete-account">
      <form class="stack" @submit.prevent="remove"><h2 id="delete-account">Удалить аккаунт?</h2>
        <p>Будут удалены диалоги, файлы, сохранённые факты и настройки.</p>
        <label>Подтвердите паролем <input v-model="removePassword" type="password" autocomplete="current-password" required></label>
        <p v-if="deletionError" class="notice danger" role="alert">{{ deletionError }}</p>
        <div class="actions"><button type="button" class="secondary" autofocus @click="dialog?.close()">Отмена</button>
          <button type="submit" class="danger-button" :disabled="busy">Удалить аккаунт</button></div>
      </form>
    </dialog>
  </section>
</template>
