<script setup lang="ts">
import { computed, watch } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { useSession } from './session'
import AppHeader from './components/AppHeader.vue'
import UiToast from './components/ui/UiToast.vue'

const account = useSession()
const route = useRoute()
const router = useRouter()
const fullScreen = computed(() => route.name === 'login' || route.name === 'onboarding')
watch(() => account.account, (value, previous) => {
  if (!value && previous && route.name !== 'login') void router.replace('/login')
})
</script>

<template>
  <a class="skip-link" href="#main">Перейти к содержимому</a>
  <AppHeader v-if="!fullScreen && account.account" :account="account.account" />
  <main id="main" tabindex="-1">
    <RouterView />
  </main>
  <UiToast />
</template>
