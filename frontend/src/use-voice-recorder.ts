import { computed, onScopeDispose, ref, shallowRef } from 'vue'

export type VoiceRecordingLimits = { maxSeconds: number; maxBytes: number; mimeTypes?: string[] }
export const defaultVoiceRecordingLimits: VoiceRecordingLimits = { maxSeconds: 120, maxBytes: 12 * 1024 * 1024 }
export type VoiceClip = { audio: Blob; url: string; seconds: number }
type Phase = 'idle' | 'requesting' | 'recording' | 'finishing' | 'preview'
type Capture = {
  recorder: MediaRecorder; stream: MediaStream; limits: VoiceRecordingLimits; chunks: Blob[]
  onTrackEnded?: () => void
  started: number; bytes: number; tick?: number; deadline?: number
}
const formats = ['audio/webm;codecs=opus', 'audio/mp4', 'audio/ogg;codecs=opus']

function releaseTracks(stream: MediaStream) {
  stream.getTracks().forEach(track => track.stop())
}

function microphoneError(reason: unknown) {
  switch (reason instanceof Error ? reason.name : '') {
    case 'NotAllowedError':
    case 'SecurityError': return 'Доступ к микрофону запрещён. Разрешите его в настройках сайта и повторите.'
    case 'NotFoundError': return 'Микрофон не найден. Подключите устройство и повторите.'
    case 'NotReadableError':
    case 'AbortError': return 'Микрофон занят или недоступен. Закройте другие приложения и повторите.'
    default: return 'Не удалось начать запись. Проверьте микрофон и повторите.'
  }
}

export function useVoiceRecorder(readLimits: () => VoiceRecordingLimits = () => defaultVoiceRecordingLimits) {
  const phase = ref<Phase>('idle')
  const clip = shallowRef<VoiceClip>()
  const seconds = ref(0)
  const error = ref('')
  const unfinished = computed(() => phase.value !== 'idle')
  let sequence = 0
  let active: Capture | undefined

  function removeClip() {
    if (clip.value) URL.revokeObjectURL(clip.value.url)
    clip.value = undefined
  }
  function clearTimers(capture: Capture) {
    window.clearInterval(capture.tick)
    window.clearTimeout(capture.deadline)
  }
  function releaseCapture(capture: Capture) {
    clearTimers(capture)
    capture.recorder.ondataavailable = null
    capture.recorder.onstop = null
    capture.recorder.onerror = null
    const onEnded = capture.onTrackEnded
    if (onEnded) capture.stream.getAudioTracks().forEach(track => track.removeEventListener('ended', onEnded))
    releaseTracks(capture.stream)
    capture.chunks.length = 0
  }
  function discard(message = '') {
    sequence++
    const capture = active
    active = undefined
    if (capture) {
      if (capture.recorder.state !== 'inactive') capture.recorder.stop()
      releaseCapture(capture)
    }
    error.value = message
    phase.value = clip.value ? 'preview' : 'idle'
  }
  function reset() {
    removeClip()
    discard()
  }
  function elapsed(capture: Capture) {
    return Math.min(capture.limits.maxSeconds, (performance.now() - capture.started) / 1000)
  }
  function finish() {
    const capture = active
    if (!capture || phase.value !== 'recording') return
    phase.value = 'finishing'
    seconds.value = elapsed(capture)
    clearTimers(capture)
    if (capture.recorder.state !== 'inactive') capture.recorder.stop()
    releaseTracks(capture.stream)
  }
  function finishCapture(capture: Capture) {
    if (active !== capture) return
    const audio = new Blob(capture.chunks, { type: capture.recorder.mimeType || capture.chunks[0]?.type })
    if (!audio.size) { discard('В записи нет звука. Попробуйте записать заново.'); return }
    if (audio.size > capture.limits.maxBytes) { discard('Запись превышает допустимый размер. Запишите более короткий фрагмент.'); return }
    const duration = phase.value === 'finishing' ? seconds.value : elapsed(capture)
    releaseCapture(capture)
    active = undefined
    removeClip()
    clip.value = { audio, url: URL.createObjectURL(audio), seconds: duration }
    phase.value = 'preview'
  }
  async function start() {
    if (phase.value !== 'idle' && phase.value !== 'preview') return
    error.value = ''
    if (!window.isSecureContext) {
      error.value = 'Для записи голоса откройте Budz по HTTPS.'
      return
    }
    const limits = readLimits()
    const type = typeof MediaRecorder !== 'undefined' && (limits.mimeTypes ?? formats)
      .find(format => MediaRecorder.isTypeSupported(format))
    if (!navigator.mediaDevices?.getUserMedia || !type) {
      error.value = 'Запись голоса недоступна в этом браузере. Вы можете продолжить текстом.'
      return
    }
    const request = ++sequence
    phase.value = 'requesting'
    seconds.value = 0
    let stream: MediaStream | undefined
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      if (request !== sequence) { releaseTracks(stream); return }
      const recorder = new MediaRecorder(stream, { mimeType: type, audioBitsPerSecond: 64000 })
      const capture: Capture = { recorder, stream, limits, chunks: [], started: performance.now(), bytes: 0 }
      active = capture
      recorder.ondataavailable = ({ data }) => {
        if (active !== capture || !data.size) return
        capture.bytes += data.size
        if (capture.bytes > capture.limits.maxBytes) {
          discard('Запись превышает допустимый размер. Запишите более короткий фрагмент.')
        } else capture.chunks.push(data)
      }
      recorder.onstop = () => finishCapture(capture)
      recorder.onerror = () => {
        if (active === capture) discard('Запись прервалась. Проверьте микрофон и повторите.')
      }
      const onTrackEnded = () => {
        if (active === capture && phase.value === 'recording') discard('Микрофон отключился. Подключите его и запишите заново.')
      }
      capture.onTrackEnded = onTrackEnded
      stream.getAudioTracks().forEach(track => track.addEventListener('ended', onTrackEnded, { once: true }))
      recorder.start(250)
      phase.value = 'recording'
      capture.tick = window.setInterval(() => { seconds.value = elapsed(capture) }, 250)
      capture.deadline = window.setTimeout(finish, capture.limits.maxSeconds * 1000)
    } catch (reason) {
      if (request === sequence) {
        if (stream && !active) releaseTracks(stream)
        discard(microphoneError(reason))
      }
    }
  }
  function onVisibilityChange() {
    if (document.hidden && (phase.value === 'requesting' || phase.value === 'recording')) {
      discard('Запись прервана при уходе из вкладки. Можно записать заново.')
    }
  }
  document.addEventListener('visibilitychange', onVisibilityChange)
  window.addEventListener('pagehide', reset)
  onScopeDispose(() => {
    document.removeEventListener('visibilitychange', onVisibilityChange)
    window.removeEventListener('pagehide', reset)
    reset()
  })
  return { phase, clip, seconds, error, unfinished, start, finish, discard, reset }
}

export type VoiceRecorder = ReturnType<typeof useVoiceRecorder>
