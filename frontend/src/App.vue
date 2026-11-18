<script setup lang="ts">
import { computed, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { useSession } from './session'

const account = useSession()
const route = useRoute()
const router = useRouter()
const fullScreen = computed(() => route.name === 'login' || route.name === 'onboarding')
const nav = [
  { to: '/chats', label: 'Диалоги' },
  { to: '/personalization', label: 'Персонализация' },
  { to: '/memory', label: 'Память' },
  { to: '/personas', label: 'Персоны' },
  { to: '/plans', label: 'Тарифы' },
]
watch(() => account.account, (value, previous) => {
  if (!value && previous && route.name !== 'login') void router.replace('/login')
})
</script>

<template>
  <a class="skip-link" href="#main">Перейти к содержимому</a>
  <header v-if="!fullScreen && account.account" class="topbar">
    <RouterLink class="brand" to="/chats" aria-label="AI Helper Budz, диалоги">
      <span class="brand-mark" aria-hidden="true">b<span>.</span></span><span>budz<span class="brand-dot">.</span></span>
    </RouterLink>
    <nav aria-label="Основная навигация">
      <RouterLink v-for="item in nav" :key="item.to" :to="item.to">{{ item.label }}</RouterLink>
    </nav>
    <RouterLink class="account-link" to="/profile">
      <span class="avatar" aria-hidden="true">{{ account.account.username.slice(0, 1).toUpperCase() }}</span>
      <span class="account-name">{{ account.account.username }}</span>
      <span class="sr-only">Профиль</span>
    </RouterLink>
  </header>
  <main id="main" :class="{ 'workspace-main': route.name === 'chat' }" tabindex="-1">
    <RouterView />
  </main>
</template>
