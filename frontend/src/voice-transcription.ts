import { onScopeDispose, ref, watch, type ShallowRef } from 'vue'
import { api, ApiError, explain } from './api'
import type { VoiceRecorder, VoiceRecordingLimits } from './use-voice-recorder'

type Language = 'auto' | 'ru'
type Capabilities = VoiceRecordingLimits & {
  available: boolean; startsPerMinute: number; timeoutSeconds: number; languages: Language[]
}
type Transcript = { text: string; language?: string | null }
export function useVoiceTranscription(voice: VoiceRecorder, capabilities: ShallowRef<Capabilities | undefined>) {
  const text = ref('')
  const language = ref<Language>('auto')
  const detectedLanguage = ref<string | null>()
  const pending = ref(false)
  const slow = ref(false)
  const error = ref('')
  const loadingCapabilities = ref(true)
  const firstUse = ref(true)
  let request: AbortController | undefined
  let settingsRequest: AbortController | undefined
  let slowTimer: number | undefined

  function cancel() {
    request?.abort()
    request = undefined
    window.clearTimeout(slowTimer)
    pending.value = slow.value = false
  }
  function clear() {
    cancel()
    text.value = error.value = ''
    detectedLanguage.value = undefined
  }
  async function loadCapabilities() {
    settingsRequest?.abort()
    const controller = new AbortController()
    settingsRequest = controller
    loadingCapabilities.value = true
    const deadline = window.setTimeout(() => controller.abort(), 1800)
    try {
      const result = await api<Capabilities>('/voice/capabilities', { signal: controller.signal })
      if (!controller.signal.aborted) capabilities.value = result
    } catch {
      if (!controller.signal.aborted) capabilities.value = undefined
    } finally {
      window.clearTimeout(deadline)
      if (settingsRequest === controller) loadingCapabilities.value = false
    }
  }
  async function transcribe() {
    const clip = voice.clip.value
    if (!clip || pending.value || !capabilities.value?.available) return
    error.value = ''
    firstUse.value = false
    const controller = new AbortController()
    request = controller
    pending.value = true
    slowTimer = window.setTimeout(() => { slow.value = true }, 6500)
    const body = new FormData()
    const extension = clip.audio.type.startsWith('audio/mp4') ? 'mp4' : clip.audio.type.startsWith('audio/ogg') ? 'ogg' : 'webm'
    body.set('audio', clip.audio, `recording.${extension}`)
    body.set('language', language.value)
    try {
      const result = await api<Transcript>('/voice/transcriptions', { method: 'POST', body, signal: controller.signal })
      if (request !== controller || clip !== voice.clip.value) return
      const resultText = typeof result.text === 'string' ? result.text.trim() : ''
      if (!resultText) { error.value = 'В записи не разобрана речь. Можно прослушать её или записать заново.'; return }
      text.value = resultText
      detectedLanguage.value = result.language ?? null
      return resultText
    } catch (reason) {
      if (request !== controller) return
      error.value = reason instanceof ApiError && reason.code === 'voice_timeout'
        ? 'Распознавание заняло слишком много времени. Можно повторить.' : explain(reason)
    } finally {
      if (request === controller) cancel()
    }
  }

  watch(voice.clip, clear, { flush: 'sync' })
  onScopeDispose(() => { clear(); settingsRequest?.abort() })
  return { text, language, detectedLanguage, pending, slow, error, capabilities, loadingCapabilities, firstUse,
    transcribe, cancel, clear, loadCapabilities }
}

export type VoiceTranscription = ReturnType<typeof useVoiceTranscription>
export type VoiceCapabilities = Capabilities
