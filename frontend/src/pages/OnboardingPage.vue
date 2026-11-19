<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, explain } from '../api'
import { useSession } from '../session'
import ProfileFields from '../components/ProfileFields.vue'
import ThemePicker from '../components/ThemePicker.vue'
import type { Profile } from '../types'

const profile = ref<Profile>()
const router = useRouter()
const session = useSession()
const error = ref('')
const busy = ref(false)
const step = ref(0)
const steps = [
  { title: 'Как должен выглядеть Budz?', section: null },
  { title: 'Как вам отвечать?', section: 'reply' },
  { title: 'Что стоит учитывать?', section: 'interests' },
  { title: 'Разрешить автоматическую память?', section: 'memory' },
] as const
const lastStep = steps.length - 1
const currentStep = computed(() => steps[step.value])
async function load() {
  error.value = ''
  try { profile.value = await api<Profile>('/me/personalization') }
  catch (reason) { error.value = explain(reason) }
}
async function finish() {
  if (!profile.value) return
  error.value = ''; busy.value = true
  try {
    await api<void>('/me/personalization', { method: 'PUT', body: {
      ...profile.value, interests: profile.value.interests.map((item) => item.trim()).filter(Boolean), onboardingCompleted: true,
    } })
    await session.reload()
    await router.replace('/chats')
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="page narrow-page">
    <div class="brand"><span class="brand-mark" aria-hidden="true">b<span>.</span></span>budz.</div>
    <header class="page-heading"><p class="eyebrow">Шаг {{ step + 1 }} из {{ steps.length }}</p><h1>{{ currentStep.title }}</h1></header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button v-if="!profile" class="text-button" @click="load">Повторить</button></p>
    <form v-if="profile" class="stack" @submit.prevent="step < lastStep ? step++ : finish()">
      <ThemePicker v-if="!currentStep.section" />
      <ProfileFields v-else v-model="profile" :section="currentStep.section" />
      <div class="actions">
        <button v-if="step" type="button" class="secondary" @click="step--">Назад</button>
        <button type="submit" class="primary" :disabled="busy">{{ step === lastStep ? 'Начать работу' : 'Далее' }}</button>
        <button type="button" class="plain-button" :disabled="busy" @click="finish">Пропустить</button>
      </div>
    </form>
    <p v-else-if="!error" role="status">Загружаем настройки…</p>
  </section>
</template>
