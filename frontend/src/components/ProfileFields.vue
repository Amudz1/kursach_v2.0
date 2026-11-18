<script setup lang="ts">
import { computed, ref } from 'vue'
import { expertiseOptions, formatOptions, languageOptions, lengthOptions, taskOptions, toneOptions } from '../profile-options'
import type { Profile, TaskMode, TaskPreference } from '../types'

const props = withDefaults(defineProps<{ section?: 'all' | 'reply' | 'interests' | 'memory' }>(), { section: 'all' })
const profile = defineModel<Profile>({ required: true })
const interests = computed({
  get: () => profile.value.interests.join(','),
  set: (value: string) => { profile.value.interests = value.split(',') },
})
const sections = computed(() => props.section === 'all' ? ['reply', 'interests', 'rules', 'memory'] : [props.section])
const mode = ref<TaskMode>('code')
const rule = computed(() => profile.value.taskRules.find((item) => item.task === mode.value))

function changeRule(key: 'length' | 'format' | 'instructions', value: string) {
  const current: TaskPreference = rule.value ?? { task: mode.value, length: null, format: null, instructions: null }
  const next = { ...current, [key]: value || null }
  profile.value.taskRules = profile.value.taskRules.filter((item) => item.task !== mode.value)
  if (next.length || next.format || next.instructions) profile.value.taskRules.push(next)
}
</script>

<template>
  <fieldset v-if="sections.includes('reply')" class="fieldset">
    <legend>Как отвечать</legend>
    <div class="form-grid">
      <label>Язык <select v-model="profile.language" aria-label="Язык"><option v-for="(name, code) in languageOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Тон <select v-model="profile.tone" aria-label="Тон"><option v-for="(name, code) in toneOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Уровень <select v-model="profile.expertiseLevel" aria-label="Уровень"><option v-for="(name, code) in expertiseOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Формат <select v-model="profile.responseFormat" aria-label="Формат"><option v-for="(name, code) in formatOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Длина ответа <select v-model="profile.responseLength" aria-label="Длина ответа"><option v-for="(name, code) in lengthOptions" :key="code" :value="code">{{ name }}</option></select></label>
    </div>
  </fieldset>
  <fieldset v-if="sections.includes('interests')" class="fieldset">
    <legend>Что учитывать</legend>
    <div class="stack">
      <label>Интересы <input v-model="interests" placeholder="Учёба, C#, дизайн" maxlength="480" aria-describedby="interests-help"></label>
      <p id="interests-help" class="help">До 12 интересов, через запятую.</p>
      <label>О себе <textarea v-model="profile.about" rows="4" maxlength="1000" placeholder="Например, я изучаю .NET и предпочитаю примеры из практики."></textarea></label>
    </div>
  </fieldset>
  <fieldset v-if="sections.includes('rules')" class="fieldset adaptive-rules stack">
    <legend>Контекст и правила задач</legend>
    <label>Текущая цель <input v-model="profile.goal" maxlength="300" placeholder="Например, подготовиться к защите диплома"></label>
    <label class="check"><input v-model="profile.adaptToTask" type="checkbox"> Подбирать подход по типу запроса</label>
    <p class="help">Budz распознаёт код, обучение, работу с текстом и планирование. Короткие уточнения могут учитывать предыдущий вопрос.</p>
    <template v-if="profile.adaptToTask">
      <div class="task-switch" aria-label="Тип задачи">
        <button v-for="(title, task) in taskOptions" :key="task" type="button" :class="mode === task ? 'primary' : 'secondary'"
          :aria-pressed="mode === task" @click="mode = task">{{ title }}</button>
      </div>
      <div class="form-grid">
        <label>Длина для задачи <select :value="rule?.length ?? ''" aria-label="Длина для задачи"
          @change="changeRule('length', ($event.target as HTMLSelectElement).value)">
          <option value="">Как в профиле</option><option v-for="(name, code) in lengthOptions" :key="code" :value="code">{{ name }}</option>
        </select></label>
        <label>Формат для задачи <select :value="rule?.format ?? ''" aria-label="Формат для задачи"
          @change="changeRule('format', ($event.target as HTMLSelectElement).value)">
          <option value="">Как в профиле</option><option v-for="(name, code) in formatOptions" :key="code" :value="code">{{ name }}</option>
        </select></label>
      </div>
      <label>Предпочтение для задачи <textarea :value="rule?.instructions ?? ''" rows="2" maxlength="300"
        placeholder="Например, приводи примеры на C# и объясняй пограничные случаи."
        @input="changeRule('instructions', ($event.target as HTMLTextAreaElement).value)"></textarea></label>
    </template>
  </fieldset>
  <fieldset v-if="sections.includes('memory')" class="fieldset">
    <legend>Что запоминать</legend>
    <label class="check"><input v-model="profile.autoMemoryEnabled" type="checkbox"> Автоматически сохранять устойчивые факты из диалогов</label>
    <label class="check memory-reply-setting"><input v-model="profile.useMemoryInReplies" type="checkbox"> Учитывать подходящие факты в ответах</label>
    <p class="help">Вы можете редактировать и удалять сохранённые факты в разделе «Память».</p>
  </fieldset>
</template>
