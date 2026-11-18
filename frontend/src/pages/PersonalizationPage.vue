<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, explain } from '../api'
import ProfileFields from '../components/ProfileFields.vue'
import ProfilePreview from '../components/ProfilePreview.vue'
import type { Profile } from '../types'

const profile = ref<Profile>()
const error = ref('')
const status = ref('')
const busy = ref(false)

async function load() {
  error.value = ''
  try { profile.value = await api<Profile>('/me/personalization') }
  catch (reason) { error.value = explain(reason) }
}

async function save() {
  if (!profile.value) return
  error.value = ''; status.value = ''; busy.value = true
  try {
    const next = { ...profile.value, interests: profile.value.interests.map((item) => item.trim()).filter(Boolean) }
    await api<void>('/me/personalization', { method: 'PUT', body: next })
    profile.value = next
    status.value = 'Настройки сохранены. Следующий ответ учтёт изменения.'
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading"><h1>Персонализация</h1><p>Настройте, как Budz объясняет, оформляет и запоминает.</p></header>
    <a v-if="profile" class="text-button preview-jump" href="#response-preview">К предпросмотру ↓</a>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button v-if="!profile" class="text-button" @click="load">Повторить</button></p>
    <p v-if="!profile && !error" role="status" class="muted">Загружаем настройки…</p>
    <div v-if="profile" class="profile-grid">
      <form class="stack" @submit.prevent="save">
        <ProfileFields v-model="profile" />
        <button type="submit" class="primary self-start" :disabled="busy">{{ busy ? 'Сохраняем…' : 'Сохранить настройки' }}</button>
        <p v-if="status" class="notice success" role="status">{{ status }}</p>
      </form>
      <ProfilePreview :profile="profile" />
    </div>
  </section>
</template>
