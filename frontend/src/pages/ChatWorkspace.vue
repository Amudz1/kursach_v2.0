<script setup lang="ts">
import { effectScope, onBeforeUnmount, ref, shallowRef, watch, type Component } from 'vue'
import { onBeforeRouteUpdate } from 'vue-router'
import ChatPage from './ChatPage.vue'
import UiButton from '../components/ui/UiButton.vue'
import UiDialog from '../components/ui/UiDialog.vue'
import { useSession } from '../session'
import { useChat } from '../use-chat'
import { registerInterfaceSwitchGuard } from '../interface-switch'
import type { LoadVoiceSession, VoiceSession } from '../voice-session'

const session = useSession()
const chat = useChat()
const voice = shallowRef<VoiceSession>()
const voiceScope = effectScope(true)
let loadingVoice: Promise<VoiceSession | undefined> | undefined
const confirmVoiceOpen = ref(false)
let resolveDeparture: ((proceed: boolean) => void) | undefined

const loadVoice: LoadVoiceSession = () => {
  if (loadingVoice) return loadingVoice
  if (voice.value) return Promise.resolve(voice.value)
  loadingVoice = import('../voice-session').then(async ({ createVoiceSession }) => {
    const loaded = voiceScope.active ? voiceScope.run(createVoiceSession) : undefined
    if (!loaded) return undefined
    voice.value = loaded
    await loaded.transcription.loadCapabilities()
    return voiceScope.active ? loaded : undefined
  }).finally(() => { loadingVoice = undefined })
  return loadingVoice
}

function resetVoice() {
  voice.value?.reset()
}
function decideVoiceDeparture(proceed: boolean) {
  if (proceed) resetVoice()
  confirmVoiceOpen.value = false
  resolveDeparture?.(proceed)
  resolveDeparture = undefined
}
function confirmVoiceDeparture(): Promise<boolean> {
  if (!voice.value?.recorder.unfinished.value) return Promise.resolve(true)
  resolveDeparture?.(false)
  confirmVoiceOpen.value = true
  return new Promise(resolve => { resolveDeparture = resolve })
}
const unregisterSwitchGuard = registerInterfaceSwitchGuard(() => voice.value?.recorder.unfinished.value ? resetVoice : undefined)
onBeforeRouteUpdate((to, from) => {
  if (to.params.id !== from.params.id || (!to.params.id && to.query.workspace !== from.query.workspace)) {
    return confirmVoiceDeparture()
  }
})
watch([chat.selectedId, chat.workspaceId], ([id, workspace], [previousId, previousWorkspace]) => {
  if (id !== previousId || (!id && workspace !== previousWorkspace)) resetVoice()
})
const focusPage = shallowRef<Component>()
const loadingFocus = ref(false)
const focusFailed = ref(false)
let attempt = 0

// Use a separate import URL for the retry because browsers can cache a failed module request.
const retryFocus = import.meta.glob<Component>('./FocusChatPage.vue', {
  query: '?retry', import: 'default',
})['./FocusChatPage.vue']

async function loadFocus(retry = false) {
  if (loadingFocus.value || focusPage.value) return
  const request = ++attempt
  focusFailed.value = false
  loadingFocus.value = true
  try {
    const view = retry ? await retryFocus() : (await import('./FocusChatPage.vue')).default
    if (request === attempt) focusPage.value = view
  } catch {
    if (request === attempt) focusFailed.value = true
  } finally {
    if (request === attempt) loadingFocus.value = false
  }
}

watch(() => session.interfaceVariant, (variant) => {
  if (variant === 'focus') { void loadFocus(); return }
  attempt++
  loadingFocus.value = focusFailed.value = false
}, { immediate: true })
onBeforeUnmount(() => {
  attempt++
  unregisterSwitchGuard()
  voiceScope.stop()
  decideVoiceDeparture(false)
})
</script>

<template>
  <component v-if="session.interfaceVariant === 'focus' && focusPage" :is="focusPage" :chat="chat" :voice="voice"
    :load-voice="loadVoice" :confirm-voice-departure="confirmVoiceDeparture" />
  <ChatPage v-else :chat="chat" :voice="voice" :load-voice="loadVoice"
    :confirm-voice-departure="confirmVoiceDeparture" data-interface="current" />
  <div v-if="focusFailed" class="notice danger interface-notice" role="alert">
    <p>Интерфейс не загрузился.</p>
    <div class="actions">
      <UiButton @click="loadFocus(true)">Повторить</UiButton>
      <UiButton @click="session.useCurrentInterfaceForSession()">Открыть текущий Budz</UiButton>
    </div>
  </div>
  <p v-else-if="loadingFocus" class="notice interface-notice" role="status">Загружаем интерфейс «Фокус»…</p>
  <UiDialog :model-value="confirmVoiceOpen" title="Перейти в другой диалог?" size="sm"
    description="Незавершённая голосовая операция будет прервана."
    @update:model-value="decideVoiceDeparture(false)">
    <template #footer>
      <UiButton autofocus @click="decideVoiceDeparture(false)">Остаться</UiButton>
      <UiButton variant="danger" @click="decideVoiceDeparture(true)">Прервать и переключить</UiButton>
    </template>
  </UiDialog>
</template>

<style scoped>
.interface-notice { position: fixed; z-index: 5; inset-block-start: calc(var(--header) + var(--space-2));
  inset-inline-end: var(--space-2); max-width: calc(100vw - 2 * var(--space-2));
  display: grid; gap: var(--space-2); box-shadow: var(--elevation-2); background: var(--surface-1); }
</style>
