<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api, explain } from '../api'
import { expertiseOptions, formatOptions, labelFor, languageOptions, lengthOptions, taskOptions, toneOptions } from '../profile-options'
import type { Profile, ResponseDecision } from '../types'
const props = defineProps<{ profile: Profile }>()
const sample = ref('')
const decision = ref<ResponseDecision>()
const error = ref('')
const busy = ref(false)
let revision = 0
watch(() => props.profile, () => { revision++; decision.value = undefined }, { deep: true, flush: 'sync' })
watch(sample, () => { revision++; decision.value = undefined }, { flush: 'sync' })

async function inspect() {
  if (!sample.value.trim()) return
  const requested = revision
  error.value = ''; busy.value = true
  try {
    const profile = { ...props.profile, interests: props.profile.interests.map((item) => item.trim()).filter(Boolean) }
    const result = await api<ResponseDecision>('/me/personalization/preview', { method: 'POST', body: { profile, sample: sample.value } })
    if (requested === revision) decision.value = result
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}

const examples = computed(() => {
  const { language, tone, expertiseLevel, responseLength, responseFormat } = props.profile
  const en = language === 'en'
  const lead = en
    ? (tone === 'formal' ? 'A collection groups related elements.' : 'A collection keeps things together.')
    : (tone === 'formal' ? 'Коллекция объединяет связанные элементы.' : 'Коллекция помогает хранить связанные вещи вместе.')
  const detail = expertiseLevel === 'expert'
    ? (en ? 'A list offers indexed access. A dictionary prioritizes key lookups.' : 'Список даёт доступ по индексу, словарь оптимизирован для поиска по ключу.')
    : (en ? 'A list of books is a familiar example.' : 'Например, список книг, которые вы хотите прочитать.')
  const next = en ? 'Choose the collection based on how you need to find items.' : 'Выбор коллекции зависит от того, как вы планируете искать элементы.'
  const parts = responseLength === 'short' ? [lead] : responseLength === 'long' ? [lead, detail, next] : [lead, detail]
  return { en, parts, format: responseFormat }
})
</script>

<template>
  <aside id="response-preview" class="preview-panel" aria-label="Предпросмотр ответа" tabindex="-1">
    <div class="preview-title">
      <span class="preview-symbol" aria-hidden="true">↳</span>
      <div><h2>Предпросмотр ответа</h2><p class="muted">Пример для текущих настроек</p></div>
    </div>
    <dl class="context-list">
      <div><dt>Язык</dt><dd>{{ labelFor(languageOptions, profile.language) }}</dd></div>
      <div><dt>Тон</dt><dd>{{ labelFor(toneOptions, profile.tone) }}</dd></div>
      <div><dt>Уровень</dt><dd>{{ labelFor(expertiseOptions, profile.expertiseLevel) }}</dd></div>
      <div><dt>Формат</dt><dd>{{ labelFor(formatOptions, profile.responseFormat) }}</dd></div>
      <div><dt>Длина</dt><dd>{{ labelFor(lengthOptions, profile.responseLength) }}</dd></div>
    </dl>
    <div class="preview-dialog">
      <p class="preview-question">{{ examples.en ? 'What is a collection?' : 'Что такое коллекция?' }}</p>
      <ol v-if="examples.format === 'steps'"><li v-for="part in examples.parts" :key="part">{{ part }}</li></ol>
      <ul v-else-if="examples.format === 'lists'"><li v-for="part in examples.parts" :key="part">{{ part }}</li></ul>
      <template v-else>
        <p v-for="part in examples.parts" :key="part">{{ part }}</p>
        <pre v-if="examples.format === 'code'"><code>var books = new List&lt;string&gt;();</code></pre>
      </template>
    </div>
    <form class="policy-preview stack" @submit.prevent="inspect">
      <label>Пример запроса <input v-model="sample" maxlength="400" placeholder="Проверь мою функцию на C#"></label>
      <button class="secondary self-start" type="submit" :disabled="busy || !sample.trim()">Проверить правила</button>
      <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
      <div v-if="decision" class="policy-result" role="status">
        <strong>{{ taskOptions[decision.task] }}</strong>
        <span>{{ labelFor(lengthOptions, decision.length) }} · {{ labelFor(formatOptions, decision.format) }}</span>
        <p v-if="decision.rule">{{ decision.rule }}</p>
        <p v-if="decision.instructions">{{ decision.instructions }}</p>
      </div>
    </form>
  </aside>
</template>
