<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api, explain } from '../api'
import { expertiseOptions, formatOptions, labelFor, lengthOptions, taskOptions, toneOptions } from '../profile-options'
import type { Profile, ResponseDecision } from '../types'

const props = defineProps<{ profile: Profile }>()
const mode = ref<'example' | 'request'>('example')
const sample = ref('')
const previous = ref('')
const decision = ref<ResponseDecision>()
const error = ref('')
const busy = ref(false)
let revision = 0

watch([() => props.profile, sample, previous], () => {
  revision++
  decision.value = undefined
  error.value = ''
}, { deep: true, flush: 'sync' })

async function inspect() {
  if (busy.value || !sample.value.trim()) return
  const requested = revision
  error.value = ''
  busy.value = true
  try {
    const result = await api<ResponseDecision>('/me/personalization/preview', {
      method: 'POST', body: { profile: props.profile, sample: sample.value, previous: previous.value.trim() || null },
    })
    if (requested === revision) decision.value = result
  } catch (reason) {
    if (requested === revision) error.value = explain(reason)
  } finally { busy.value = false }
}

const examples = computed(() => {
  const { language, tone, expertiseLevel, responseLength } = props.profile
  const en = language === 'en'
  const leads = en ? {
    formal: 'A collection is a structure for storing a group of elements.',
    friendly: 'Think of a collection as a shelf where you keep related things together.',
    concise: 'A collection stores a group of items.',
    detailed: 'A collection groups elements and defines how you can add, remove or look them up.',
  } : {
    formal: 'Коллекция — структура для хранения группы элементов.',
    friendly: 'Представьте полку, на которой вы держите связанные вещи вместе. Так работает коллекция.',
    concise: 'Коллекция хранит группу элементов.',
    detailed: 'Коллекция объединяет элементы и задаёт способы их добавления, удаления и поиска.',
  }
  const details = en ? {
    beginner: 'A list of books is a simple example. You can add a book or read the list in order.',
    intermediate: 'In C#, choose a List for ordered items or a Dictionary to look up values by key.',
    expert: 'A list offers constant-time indexed access. A dictionary usually offers constant-time key lookups.',
  } : {
    beginner: 'Простой пример — список книг. В него можно добавить книгу и прочитать все записи по порядку.',
    intermediate: 'В C# для упорядоченных элементов подходит List, а для поиска значений по ключу — Dictionary.',
    expert: 'Список даёт доступ по индексу за O(1). Для словаря поиск по ключу обычно тоже занимает O(1).',
  }
  const next = en ? 'Choose based on how you need to access the items.' : 'При выборе коллекции решите, как вы будете получать элементы.'
  const parts = [labelFor(leads, tone), labelFor(details, expertiseLevel), next]
  const count = responseLength === 'short' ? 1 : responseLength === 'long' ? 3 : 2
  return { en, parts: parts.slice(0, count) }
})
</script>

<template>
  <aside id="response-preview" class="response-check" aria-labelledby="preview-heading" tabindex="-1">
    <header>
      <h2 id="preview-heading">Проверка ответа</h2>
      <p>Посмотрите, как изменения повлияют на ответы, до сохранения.</p>
    </header>
    <div class="preview-switch" role="group" aria-label="Режим проверки">
      <button type="button" :aria-pressed="mode === 'example'" @click="mode = 'example'">Пример стиля</button>
      <button type="button" :aria-pressed="mode === 'request'" @click="mode = 'request'">Свой запрос</button>
    </div>
    <div v-show="mode === 'example'" class="example-card">
      <p class="example-question" :lang="examples.en ? 'en' : 'ru'">{{ examples.en ? 'What is a collection?' : 'Что такое коллекция?' }}</p>
      <div class="example-reply" :lang="examples.en ? 'en' : 'ru'">
        <ol v-if="profile.responseFormat === 'steps'"><li v-for="part in examples.parts" :key="part">{{ part }}</li></ol>
        <ul v-else-if="profile.responseFormat === 'lists'"><li v-for="part in examples.parts" :key="part">{{ part }}</li></ul>
        <template v-else>
          <p v-for="part in examples.parts" :key="part">{{ part }}</p>
          <pre v-if="profile.responseFormat === 'code'"><code>var books = new List&lt;string&gt;();</code></pre>
        </template>
      </div>
      <p class="example-meta">{{ labelFor(toneOptions, profile.tone) }} · {{ labelFor(expertiseOptions, profile.expertiseLevel) }} · {{ labelFor(lengthOptions, profile.responseLength) }}</p>
    </div>
    <form v-show="mode === 'request'" class="request-check stack" @submit.prevent="inspect">
      <label>Ваш запрос <textarea v-model="sample" maxlength="400" rows="2" placeholder="Проверь мою функцию на C#"></textarea></label>
      <details>
        <summary>Добавить предыдущий запрос</summary>
        <label>Предыдущий запрос <textarea v-model="previous" maxlength="400" rows="2" placeholder="Для уточнений вроде «А подробнее?»"></textarea></label>
      </details>
      <button class="secondary self-start" type="submit" :disabled="busy || !sample.trim()">{{ busy ? 'Проверяем…' : 'Проверить правила' }}</button>
      <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
      <div v-if="decision" class="decision-card" role="status">
        <h3>{{ taskOptions[decision.task] }}</h3>
        <p class="decision-format">{{ labelFor(lengthOptions, decision.length) }} · {{ labelFor(formatOptions, decision.format) }}</p>
        <p v-if="decision.fromHistory" class="help">Тип задачи определён по предыдущему запросу.</p>
        <p v-if="decision.rule" class="help">{{ decision.rule }}</p>
        <p v-if="decision.instructions" class="decision-instruction">{{ decision.instructions }}</p>
      </div>
    </form>
  </aside>
</template>

<style scoped>
.response-check { min-width: 0; padding: clamp(.9rem, 2.4vw, 1.5rem); background: var(--feature); color: #fffdf5;
  border-radius: 13px; scroll-margin-top: calc(var(--header) + .9rem); }
.response-check header h2 { font: 500 clamp(1.65rem, 2.5vw, 2.2rem)/1.1 var(--serif); letter-spacing: -.037em; }
.response-check header p { margin-top: .45rem; font-size: .86rem; color: #e3eadd; }
.preview-switch { display: grid; grid-template-columns: 1fr 1fr; gap: .2rem; padding: .23rem; margin: 1.05rem 0 .75rem;
  background: #1e352a; border-radius: 8px; }
.preview-switch button { min-height: 35px; padding: .4rem .5rem; border: 0; border-radius: 6px;
  color: #f7f5ed; background: transparent; font-size: .82rem; font-weight: 650; }
.preview-switch button:hover { background: #3a5345; }
.preview-switch button[aria-pressed=true] { color: light-dark(#284135, #18251a); background: light-dark(#f4efe4, #c1dbc1); }
.response-check :focus-visible { outline-color: #f0c797; }
.example-card, .request-check { border-radius: 9px; background: var(--sheet); color: var(--ink); }
.example-card { display: grid; gap: .75rem; padding: .9rem 1rem; }
.example-question { font-size: .875rem; color: var(--accent-dark); font-weight: 680; }
.example-reply { display: grid; gap: .48rem; font-size: .91rem; line-height: 1.5; }
.example-reply ol, .example-reply ul { padding-left: 1.2rem; }
.example-reply li + li { margin-top: .28rem; }
.example-meta { border-top: 1px solid var(--rule); color: var(--muted); padding-top: .6rem; font-size: .75rem; }
.request-check { padding: .85rem; gap: .68rem; }
.request-check :focus-visible { outline-color: var(--accent); }
.request-check details summary { width: fit-content; font-size: .79rem; }
.request-check details label { margin-top: .48rem; }
.decision-card { display: grid; gap: .3rem; padding: .7rem; background: var(--pale); border-radius: 7px; }
.decision-format { color: var(--accent-dark); font-size: .86rem; font-weight: 650; }
.decision-instruction { border-top: 1px solid var(--rule); padding-top: .38rem; font-size: .86rem; }
</style>
