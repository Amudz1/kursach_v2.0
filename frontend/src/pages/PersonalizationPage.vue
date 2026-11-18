<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, explain } from '../api'
import PersonalizationRules from '../components/PersonalizationRules.vue'
import ProfilePreview from '../components/ProfilePreview.vue'
import { expertiseOptions, formatOptions, languageOptions, lengthOptions, toneOptions } from '../profile-options'
import type { Profile } from '../types'

const sections = [
  { id: 'style', label: 'Стиль ответа' },
  { id: 'rules', label: 'Правила задач' },
  { id: 'context', label: 'Контекст и память' },
] as const
const presets = [
  { name: 'Собеседник', description: 'Дружелюбный тон и обычный текст.',
    changes: { tone: 'friendly', expertiseLevel: 'intermediate', responseFormat: 'text', responseLength: 'medium' } },
  { name: 'Наставник', description: 'Подробные ответы по шагам.',
    changes: { tone: 'friendly', expertiseLevel: 'beginner', responseFormat: 'steps', responseLength: 'long' } },
  { name: 'Редактор', description: 'Коротко, по делу и списком.',
    changes: { tone: 'concise', expertiseLevel: 'intermediate', responseFormat: 'lists', responseLength: 'short' } },
] as const

const section = ref<(typeof sections)[number]['id']>('style')
const profile = ref<Profile>()
const baseline = ref<Profile>()
const error = ref('')
const status = ref('')
const busy = ref(false)
const prepared = computed(() => {
  if (!profile.value) return undefined
  return {
    ...profile.value,
    goal: profile.value.goal.trim(),
    about: profile.value.about.trim(),
    interests: profile.value.interests.map((item) => item.trim()).filter(Boolean),
  }
})
const dirty = computed(() => JSON.stringify(prepared.value) !== JSON.stringify(baseline.value))
const currentPreset = computed(() => presets.find(({ changes }) =>
  profile.value?.tone === changes.tone && profile.value.expertiseLevel === changes.expertiseLevel &&
  profile.value.responseFormat === changes.responseFormat && profile.value.responseLength === changes.responseLength))

const interests = computed({
  get: () => profile.value?.interests.join(',') ?? '',
  set: (value: string) => { if (profile.value) profile.value.interests = value.split(',') },
})

function loadDraft(saved: Profile) {
  profile.value = { ...saved, interests: [...saved.interests], taskRules: saved.taskRules.map((rule) => ({ ...rule })) }
}

async function load() {
  error.value = ''
  try {
    const saved = await api<Profile>('/me/personalization')
    baseline.value = saved
    loadDraft(saved)
  } catch (reason) { error.value = explain(reason) }
}

function reset() {
  if (baseline.value) loadDraft(baseline.value)
  error.value = ''
  status.value = ''
}

async function save() {
  if (!prepared.value || !dirty.value || busy.value) return
  error.value = ''
  status.value = ''
  busy.value = true
  try {
    await api<void>('/me/personalization', { method: 'PUT', body: prepared.value })
    const saved = await api<Profile>('/me/personalization')
    baseline.value = saved
    loadDraft(saved)
    status.value = 'Настройки сохранены.'
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="page personalization">
    <header class="settings-heading">
      <div><h1>Персонализация</h1><p>Настройте стиль ответов Budz и правила для своих задач.</p></div>
      <a v-if="profile" class="secondary preview-jump" href="#response-preview">К проверке ↓</a>
    </header>
    <p v-if="!profile && error" class="notice danger" role="alert">{{ error }} <button class="text-button" @click="load">Повторить</button></p>
    <p v-else-if="!profile" role="status" class="muted">Загружаем настройки…</p>
    <div v-if="profile && prepared" class="settings-layout">
      <form class="settings-editor" @submit.prevent="save">
        <fieldset class="settings-controls" :disabled="busy">
          <legend class="sr-only">Настройки ответов</legend>
          <div class="section-switch" role="group" aria-label="Разделы персонализации">
            <button v-for="item in sections" :key="item.id" type="button" :aria-pressed="section === item.id" @click="section = item.id">{{ item.label }}</button>
          </div>
          <section v-if="section === 'style'" class="editor-section stack" aria-labelledby="style-heading">
            <div><h2 id="style-heading">Стиль ответа</h2><p class="help">Выберите готовый стиль или настройте его вручную.</p></div>
            <div class="presets" role="group" aria-label="Готовые стили">
              <button v-for="preset in presets" :key="preset.name" type="button" :aria-label="preset.name"
                :aria-pressed="currentPreset === preset" @click="Object.assign(profile, preset.changes)">
                <strong>{{ preset.name }}</strong><span>{{ preset.description }}</span>
              </button>
            </div>
            <div class="form-grid style-fields">
              <label>Язык <select v-model="profile.language" aria-label="Язык"><option v-for="(name, code) in languageOptions" :key="code" :value="code">{{ name }}</option></select></label>
              <label>Тон <select v-model="profile.tone" aria-label="Тон"><option v-for="(name, code) in toneOptions" :key="code" :value="code">{{ name }}</option></select></label>
              <label>Уровень <select v-model="profile.expertiseLevel" aria-label="Уровень"><option v-for="(name, code) in expertiseOptions" :key="code" :value="code">{{ name }}</option></select></label>
              <label>Формат <select v-model="profile.responseFormat" aria-label="Формат"><option v-for="(name, code) in formatOptions" :key="code" :value="code">{{ name }}</option></select></label>
              <label>Длина ответа <select v-model="profile.responseLength" aria-label="Длина ответа"><option v-for="(name, code) in lengthOptions" :key="code" :value="code">{{ name }}</option></select></label>
            </div>
          </section>
          <section v-else-if="section === 'rules'" class="editor-section stack" aria-labelledby="rules-heading">
            <div><h2 id="rules-heading">Правила задач</h2><p class="help">Задайте исключения из общего стиля для отдельных типов запросов.</p></div>
            <PersonalizationRules v-model="profile" />
          </section>
          <section v-else class="editor-section stack" aria-labelledby="context-heading">
            <div><h2 id="context-heading">Контекст и память</h2><p class="help">Укажите, что Budz стоит учитывать в будущих ответах.</p></div>
            <label>Текущая цель <input v-model="profile.goal" maxlength="300" placeholder="Например, подготовиться к защите диплома"></label>
            <label>О себе <textarea v-model="profile.about" rows="3" maxlength="1000" placeholder="Что вы уже умеете и какие примеры помогают вам разобраться?"></textarea></label>
            <label>Интересы <input v-model="interests" maxlength="480" placeholder="C#, базы данных, дизайн" aria-describedby="profile-interests"></label>
            <p id="profile-interests" class="help">До 12 интересов, через запятую.</p>
            <div class="memory-controls">
              <label class="check"><input v-model="profile.useMemoryInReplies" type="checkbox"> Использовать сохранённые факты в ответах</label>
              <label class="check"><input v-model="profile.autoMemoryEnabled" type="checkbox"> Автоматически запоминать факты из диалогов</label>
              <RouterLink to="/memory" class="text-button self-start">Управлять сохранёнными фактами ↗</RouterLink>
            </div>
          </section>
        </fieldset>
        <footer class="editor-footer">
          <p class="help" aria-live="polite">{{ dirty ? 'Есть несохранённые изменения' : status || 'Настройки актуальны' }}</p>
          <div class="actions">
            <button v-if="dirty" class="plain-button" type="button" :disabled="busy" @click="reset">Отменить изменения</button>
            <button class="primary" type="submit" :disabled="!dirty || busy">{{ busy ? 'Сохраняем…' : 'Сохранить настройки' }}</button>
          </div>
        </footer>
        <p v-if="error" class="notice danger form-error" role="alert">{{ error }}</p>
      </form>
      <ProfilePreview :profile="prepared" />
    </div>
  </section>
</template>

<style scoped>
.personalization { max-width: 1260px; padding-top: clamp(1.25rem, 3.2vw, 3rem); }
.settings-heading { display: flex; flex-wrap: wrap; align-items: end; justify-content: space-between; gap: .85rem; margin-bottom: 1.45rem; }
.settings-heading h1 { font: 500 clamp(2.15rem, 5.2vw, 4.4rem)/1.05 var(--serif); letter-spacing: -.047em; }
.settings-heading p { color: var(--muted); margin-top: .55rem; }
.settings-layout { display: grid; grid-template-columns: minmax(0, 1.65fr) minmax(0, 1fr); gap: 1.15rem; align-items: start; }
.settings-editor { min-width: 0; background: var(--sheet); border: 1px solid var(--rule); border-radius: 13px; }
.settings-controls { border: 0; padding: 0; margin: 0; min-width: 0; }
.section-switch { display: flex; flex-wrap: wrap; gap: .25rem .35rem; padding: .34rem clamp(.55rem, 2vw, 1.4rem) 0; border-bottom: 1px solid var(--rule); }
.section-switch button { min-height: 44px; padding: .32rem .52rem; border: 0; border-bottom: 2px solid transparent; color: var(--muted);
  background: transparent; font-size: .84rem; font-weight: 650; }
.section-switch button:hover, .section-switch button[aria-pressed=true] { color: var(--accent-dark); }
.section-switch button[aria-pressed=true] { border-bottom-color: var(--accent-dark); }
.editor-section { padding: clamp(.85rem, 2.25vw, 1.55rem); }
.editor-section h2 { margin-bottom: .32rem; font-size: clamp(1.25rem, 2.4vw, 1.75rem); }
.presets { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .45rem; }
.presets button { display: grid; gap: .25rem; align-content: start; padding: .78rem clamp(.48rem, 1.65vw, .85rem); text-align: left;
  color: var(--ink); border: 1px solid var(--rule); border-radius: 9px; background: #f6efe3; }
.presets button:hover, .presets button[aria-pressed=true] { background: var(--pale); border-color: var(--accent); }
.presets strong { font-size: .92rem; overflow-wrap: anywhere; }
.presets span { color: var(--muted); font-size: .78rem; line-height: 1.35; }
.style-fields { padding-top: .9rem; border-top: 1px solid var(--rule); }
.memory-controls { display: grid; gap: .52rem; border-top: 1px solid var(--rule); padding-top: .9rem; }
.memory-controls label { font-size: .86rem; }
.editor-footer { padding: .75rem clamp(.85rem, 2.25vw, 1.55rem); display: flex; flex-wrap: wrap; align-items: center;
  justify-content: space-between; gap: .5rem .8rem; border-top: 1px solid var(--rule); }
.form-error { margin: 0 clamp(.85rem, 2.25vw, 1.55rem) .85rem; }
.preview-jump { display: none; }
@media (max-width: 900px) {
  .settings-layout { grid-template-columns: 1fr; }
  .preview-jump { display: inline-flex; }
}
@media (max-width: 440px) {
  .section-switch { justify-content: space-between; gap: .12rem; }
  .section-switch button { padding-inline: .2rem; font-size: .74rem; }
  .presets { grid-template-columns: 1fr; }
  .presets button { grid-template-columns: minmax(0, .75fr) minmax(0, 1.3fr); align-items: baseline; }
  .editor-footer .actions { gap: .25rem; }
}
</style>
