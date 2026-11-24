<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { AudioLines, Mic, Pause, Play, RotateCcw, Square, Trash2, X } from 'lucide-vue-next'
import UiButton from './ui/UiButton.vue'
import UiDialog from './ui/UiDialog.vue'
import type { VoiceRecorder } from '../use-voice-recorder'
import type { VoiceTranscription } from '../voice-transcription'

const props = defineProps<{ recorder: VoiceRecorder; transcription: VoiceTranscription }>()
const emit = defineEmits<{ insert: [] }>()
const { phase, clip, seconds, error, start, finish, discard, reset } = props.recorder
const { text, language, detectedLanguage, pending, slow, error: transcriptError, capabilities,
  loadingCapabilities, firstUse, transcribe, cancel, loadCapabilities } = props.transcription
const confirmReplacement = ref(false)
const player = ref<HTMLAudioElement>()
const playing = ref(false)
const playerError = ref('')
const announcement = ref<HTMLElement>()
const announcedState = computed(() => {
  if (phase.value === 'requesting') return 'Разрешите доступ к микрофону'
  if (phase.value === 'recording') return 'Началась запись голоса'
  if (phase.value === 'finishing') return 'Завершаем запись'
  if (pending.value) return 'Распознаём речь'
  if (phase.value === 'preview') return detectedLanguage.value === undefined
    ? 'Голосовая запись готова для прослушивания'
    : 'Расшифровка готова. Текст можно отредактировать.'
  return ''
})
let transferFocus = false

function duration(value: number) {
  const wholeSeconds = Math.floor(value)
  return `${Math.floor(wholeSeconds / 60)}:${String(wholeSeconds % 60).padStart(2, '0')}`
}
async function togglePlayback() {
  const audio = player.value
  if (!audio) return
  playerError.value = ''
  if (!audio.paused) { audio.pause(); return }
  try { await audio.play() }
  catch { if (audio.isConnected) playerError.value = 'Не удалось воспроизвести запись.' }
}
async function recognizeRecording() {
  const recording = clip.value
  const transcript = await transcribe()
  if (transcript && recording === clip.value) emit('insert')
}
function replace() {
  confirmReplacement.value = false
  void start()
}
watch(phase, () => { playing.value = false; playerError.value = '' })
watch(clip, () => { confirmReplacement.value = false })
watch([phase, pending, detectedLanguage], async () => {
  const form = announcement.value?.closest('form')
  const previous = document.activeElement
  const hadFocus = Boolean(previous?.closest('.voice-panel, .voice-trigger') && form?.contains(previous))
  if (!hadFocus && !transferFocus) return
  transferFocus = phase.value === 'finishing'
  if (transferFocus) return
  await nextTick()
  const focus = document.activeElement
  if (focus !== document.body && focus !== previous && focus?.isConnected) return
  const target = form?.querySelector<HTMLElement>('[data-voice-next]') ??
    form?.querySelector<HTMLElement>('.voice-trigger')
  target?.focus()
})
</script>

<template>
  <span ref="announcement" class="sr-only" role="status" aria-atomic="true">{{ announcedState }}</span>
  <div v-if="phase !== 'idle' || error" class="voice-panel" aria-label="Запись голоса" role="group">
    <div v-if="phase === 'requesting'" class="voice-controls">
      <span>Разрешите доступ к микрофону</span>
      <UiButton :icon="X" size="sm" data-voice-next @click="discard()">Отменить запись</UiButton>
    </div>
    <div v-else-if="phase === 'recording'" class="voice-controls">
      <span class="voice-state"><Mic aria-hidden="true" />Идёт запись</span>
      <span role="timer" aria-live="off" aria-label="Длительность записи" class="voice-time">{{ duration(seconds) }}</span>
      <UiButton :icon="Square" size="sm" data-voice-next @click="finish">Завершить запись</UiButton>
      <UiButton :icon="X" variant="ghost" size="sm" @click="discard()">Отменить запись</UiButton>
    </div>
    <span v-else-if="phase === 'finishing'">Завершаем запись…</span>
    <div v-else-if="clip" class="voice-controls">
      <span class="voice-state">Голосовая запись</span>
      <span :aria-label="`Длительность ${duration(clip.seconds)}`" class="voice-time">{{ duration(clip.seconds) }}</span>
      <audio :key="clip.url" ref="player" :src="clip.url" preload="metadata"
        @play="playing = true" @pause="playing = false" @ended="playing = false" />
      <UiButton :icon="playing ? Pause : Play" size="sm" :data-voice-next="!pending && detectedLanguage === undefined ? '' : undefined"
        @click="togglePlayback">{{ playing ? 'Пауза' : 'Прослушать' }}</UiButton>
      <UiButton :icon="Trash2" variant="ghost" size="sm" :disabled="pending" @click="reset">Удалить запись</UiButton>
      <UiButton :icon="RotateCcw" variant="ghost" size="sm" :disabled="pending" @click="confirmReplacement = true">Записать заново</UiButton>
    </div>
    <p v-if="error || playerError" class="voice-error" role="alert">{{ error || playerError }}</p>
    <div v-if="clip && phase === 'preview'" class="voice-transcript">
      <template v-if="pending">
        <p>Распознаём речь…</p>
        <p v-if="slow" class="muted" role="status">Обработка занимает больше времени. Можно подождать или отменить.</p>
        <UiButton size="sm" data-voice-next @click="cancel">Отменить распознавание</UiButton>
      </template>
      <template v-else-if="detectedLanguage !== undefined">
        <label>Расшифровка
          <textarea v-model="text" rows="3" data-voice-next />
        </label>
        <p v-if="detectedLanguage === 'ru'" class="muted">Язык записи — русский.</p>
        <UiButton variant="primary" size="sm" :disabled="!text.trim()" @click="emit('insert')">Вставить в черновик</UiButton>
      </template>
      <template v-else-if="capabilities?.available">
        <p v-if="firstUse" class="muted">По команде «Расшифровать» запись передаётся на сервер. Текст автоматически появится в черновике. Проверьте его перед отправкой.</p>
        <div class="voice-controls">
          <label>Язык записи <select v-model="language"><option value="auto">Автоопределение</option><option value="ru">Русский</option></select></label>
          <UiButton variant="primary" :icon="AudioLines" size="sm" @click="recognizeRecording">{{ transcriptError ? 'Повторить распознавание' : 'Расшифровать' }}</UiButton>
        </div>
      </template>
      <p v-else-if="loadingCapabilities" class="muted" role="status">Проверяем доступность распознавания…</p>
      <p v-else class="muted">Распознавание речи сейчас недоступно. <button type="button" class="text-button" @click="loadCapabilities">Проверить снова</button></p>
      <p v-if="transcriptError" class="voice-error" role="alert">{{ transcriptError }}</p>
    </div>
  </div>
  <UiDialog v-if="clip" v-model="confirmReplacement" title="Заменить запись?" size="sm"
    description="Старая запись будет заменена после завершения новой.">
    <template #footer>
      <UiButton autofocus size="sm" @click="confirmReplacement = false">Оставить запись</UiButton>
      <UiButton variant="primary" size="sm" @click="replace">Записать заново</UiButton>
    </template>
  </UiDialog>
</template>

<style scoped>
.voice-panel {
  display: grid; grid-column: 1 / -1; gap: var(--space-1); min-width: 0; padding: var(--space-2);
  background: var(--surface-1); border: 1px solid var(--border); border-radius: var(--radius-md);
  color: var(--text); font-size: var(--font-size-sm);
}
.voice-controls { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); }
.voice-state { display: inline-flex; align-items: center; gap: var(--space-1); font-weight: 650; }
.voice-state svg { width: 1rem; height: 1rem; color: var(--accent); }
.voice-time { color: var(--text-muted); font-variant-numeric: tabular-nums; }
.voice-controls :deep(button) { min-height: 44px; max-width: 100%; }
.voice-error { color: var(--danger); }
.voice-transcript { display: grid; justify-items: start; gap: var(--space-2); }
.voice-transcript > label { width: 100%; }
.voice-transcript textarea { width: 100%; max-height: 8rem; resize: vertical; }
.voice-transcript :deep(button), .voice-transcript select { min-height: 44px; }
.voice-transcript :deep(.ui-button) { max-width: 100%; white-space: normal; }
.voice-panel { max-block-size: max(112px, min(18rem, calc(var(--chat-viewport-height, 100dvh) * .39))); overflow: auto; overscroll-behavior: contain; }
</style>
