<script setup lang="ts">
import { computed } from 'vue'
import { expertiseOptions, formatOptions, languageOptions, lengthOptions, toneOptions } from '../profile-options'
import type { Profile } from '../types'

defineProps<{ section: 'reply' | 'interests' | 'memory' }>()
const profile = defineModel<Profile>({ required: true })
const interests = computed({
  get: () => profile.value.interests.join(','),
  set: (value: string) => { profile.value.interests = value.split(',') },
})
</script>

<template>
  <fieldset v-if="section === 'reply'" class="fieldset">
    <legend>Как отвечать</legend>
    <div class="form-grid">
      <label>Язык <select v-model="profile.language" aria-label="Язык"><option v-for="(name, code) in languageOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Тон <select v-model="profile.tone" aria-label="Тон"><option v-for="(name, code) in toneOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Уровень <select v-model="profile.expertiseLevel" aria-label="Уровень"><option v-for="(name, code) in expertiseOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Формат <select v-model="profile.responseFormat" aria-label="Формат"><option v-for="(name, code) in formatOptions" :key="code" :value="code">{{ name }}</option></select></label>
      <label>Длина ответа <select v-model="profile.responseLength" aria-label="Длина ответа"><option v-for="(name, code) in lengthOptions" :key="code" :value="code">{{ name }}</option></select></label>
    </div>
  </fieldset>
  <fieldset v-if="section === 'interests'" class="fieldset">
    <legend>Что учитывать</legend>
    <div class="stack">
      <label>Интересы <input v-model="interests" placeholder="Учёба, C#, дизайн" maxlength="480" aria-describedby="interests-help"></label>
      <p id="interests-help" class="help">До 12 интересов, через запятую.</p>
      <label>О себе <textarea v-model="profile.about" rows="4" maxlength="1000" placeholder="Например, я изучаю .NET и предпочитаю примеры из практики."></textarea></label>
    </div>
  </fieldset>
  <fieldset v-if="section === 'memory'" class="fieldset">
    <legend>Что запоминать</legend>
    <label class="check"><input v-model="profile.autoMemoryEnabled" type="checkbox"> Автоматически сохранять устойчивые факты из диалогов</label>
    <label class="check memory-reply-setting"><input v-model="profile.useMemoryInReplies" type="checkbox"> Учитывать подходящие факты в ответах</label>
    <p class="help">Вы можете редактировать и удалять сохранённые факты в разделе «Память».</p>
  </fieldset>
</template>
