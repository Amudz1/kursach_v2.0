<script setup lang="ts">
import { computed } from 'vue'
import { expertiseOptions, formatOptions, labelFor, languageOptions, lengthOptions, toneOptions } from '../profile-options'
import type { Profile } from '../types'
const props = defineProps<{ profile: Profile }>()

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
  </aside>
</template>
