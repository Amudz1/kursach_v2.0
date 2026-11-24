<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, explain } from '../api'
import { useSession } from '../session'
import { appearance, presets, presetSettings, setAppearance, type PresetId } from '../appearance'
import ProfileFields from '../components/ProfileFields.vue'
import ThemePicker from '../components/ThemePicker.vue'
import UiProgress from '../components/ui/UiProgress.vue'
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
const onboardingPresets = ['studio', 'classic', 'ultramarine', 'mint', 'midnight'] as const
async function choosePreset(preset: PresetId) {
  error.value = ''
  try { await setAppearance(presetSettings(preset)) }
  catch (reason) { error.value = explain(reason) }
}
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
    <header class="page-heading"><p class="eyebrow">Шаг {{ step + 1 }} из {{ steps.length }}</p>
      <UiProgress class="onboarding-progress" :value="step + 1" :max="steps.length" label="Прогресс настройки" />
      <h1>{{ currentStep.title }}</h1>
    </header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button v-if="!profile" class="text-button" @click="load">Повторить</button></p>
    <form v-if="profile" class="stack" @submit.prevent="step < lastStep ? step++ : finish()">
      <template v-if="!currentStep.section">
        <div class="onboarding-presets" role="group" aria-label="Выберите оформление">
          <button v-for="preset in onboardingPresets" :key="preset" type="button" :data-choice="preset"
            :aria-pressed="appearance.preset === preset" @click="choosePreset(preset)">
            <span aria-hidden="true"><i></i><i></i></span>{{ presets[preset].label }}
          </button>
        </div>
        <ThemePicker />
      </template>
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

<style scoped>
.onboarding-progress { margin: var(--space-2) 0 var(--space-4); }
.onboarding-presets { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 115px), 1fr)); gap: var(--space-2); }
.onboarding-presets button { display: grid; gap: var(--space-2); padding: var(--space-2); min-width: 0;
  color: var(--text); background: var(--surface-1); border: 1px solid var(--border); border-radius: var(--radius-md); }
.onboarding-presets button[aria-pressed="true"] { border-color: var(--accent); box-shadow: inset 0 0 0 1px var(--accent); font-weight: 700; }
.onboarding-presets span { display: grid; gap: var(--space-1); align-content: center; height: 46px; padding: var(--space-2);
  background: var(--mini-bg, var(--surface-3)); border-radius: var(--radius-xs); }
.onboarding-presets i { height: 4px; width: 70%; border-radius: var(--radius-full); background: var(--mini-ink, var(--accent)); }
.onboarding-presets i + i { width: 45%; justify-self: end; }
[data-choice="ultramarine"] { --mini-bg: var(--c-brand-100); --mini-ink: var(--c-brand-600); }
[data-choice="studio"] { --mini-bg: var(--studio-sand); --mini-ink: var(--studio-terracotta); }
[data-choice="classic"] { --mini-bg: var(--classic-paper); --mini-ink: var(--classic-accent); }
[data-choice="mint"] { --mini-bg: honeydew; --mini-ink: var(--cat-5); }
[data-choice="midnight"] { --mini-bg: var(--c-brand-900); --mini-ink: lightblue; }
</style>
