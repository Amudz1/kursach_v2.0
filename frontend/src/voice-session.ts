import { shallowRef } from 'vue'
import VoiceRecorderPanel from './components/VoiceRecorderPanel.vue'
import { defaultVoiceRecordingLimits, useVoiceRecorder } from './use-voice-recorder'
import { useVoiceTranscription, type VoiceCapabilities } from './voice-transcription'

export function createVoiceSession() {
  const capabilities = shallowRef<VoiceCapabilities>()
  const recorder = useVoiceRecorder(() => capabilities.value ?? defaultVoiceRecordingLimits)
  const transcription = useVoiceTranscription(recorder, capabilities)
  function reset() {
    recorder.reset()
    transcription.clear()
  }
  return { panel: VoiceRecorderPanel, recorder, transcription, reset }
}

export type VoiceSession = ReturnType<typeof createVoiceSession>
export type LoadVoiceSession = () => Promise<VoiceSession | undefined>
