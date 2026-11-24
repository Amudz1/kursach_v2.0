<script setup lang="ts">
import { computed, defineAsyncComponent, watch, watchEffect } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { useSession } from './session'
import { confirmInterfaceSwitch, interfaceSwitchConfirmation } from './interface-switch'
import AppHeader from './components/AppHeader.vue'
import UiButton from './components/ui/UiButton.vue'
import UiDialog from './components/ui/UiDialog.vue'
import UiToast from './components/ui/UiToast.vue'

const account = useSession()
const route = useRoute()
const router = useRouter()
const FocusHeader = defineAsyncComponent(() => import('./components/FocusHeader.vue'))
const fullScreen = computed(() => route.name === 'login' || route.name === 'onboarding')
const focus = computed(() => !fullScreen.value && Boolean(account.account) && account.interfaceVariant === 'focus')
const closeInterfaceConfirmation = () => { interfaceSwitchConfirmation.value = undefined }
watchEffect(() => {
  if (focus.value) document.documentElement.dataset.interface = 'focus'
  else delete document.documentElement.dataset.interface
})
watch(() => account.account, (value, previous) => {
  if (value?.id !== previous?.id) closeInterfaceConfirmation()
  if (!value && previous && route.name !== 'login' && !route.meta.publicHelp) void router.replace('/login')
})
</script>

<template>
  <a class="skip-link" href="#main">Перейти к содержимому</a>
  <FocusHeader v-if="focus && account.account" :account="account.account" />
  <AppHeader v-else-if="!fullScreen && account.account" :account="account.account" />
  <main id="main" tabindex="-1">
    <RouterView />
  </main>
  <UiDialog :model-value="Boolean(interfaceSwitchConfirmation)" title="Сменить интерфейс?"
    description="Незавершённая голосовая операция будет прервана." size="sm"
    @update:model-value="closeInterfaceConfirmation">
    <template #footer>
      <UiButton variant="secondary" autofocus @click="closeInterfaceConfirmation">Остаться</UiButton>
      <UiButton variant="danger" @click="confirmInterfaceSwitch">Прервать и переключить</UiButton>
    </template>
  </UiDialog>
  <UiToast />
</template>

<style>
:root[data-interface="focus"] { --header: 56px; }
</style>
