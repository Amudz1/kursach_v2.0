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
  { to: '/workspaces', label: 'Пространства' },
  { to: '/personalization', label: 'Персонализация' },
  { to: '/memory', label: 'Память' },
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
    <nav class="top-navigation" aria-label="Основная навигация">
      <RouterLink v-for="item in nav" :key="item.to" :to="item.to"
        :aria-current="route.path === item.to ? 'page' : item.to === '/chats' && route.name === 'chat' ? 'location' : undefined">
        {{ item.label }}
      </RouterLink>
      <RouterLink v-if="account.account.role === 'Admin'" to="/admin" aria-label="Администрирование">Админ</RouterLink>
    </nav>
    <div class="header-account">
      <RouterLink class="header-balance" to="/plans" aria-label="Доступные токены и тарифы">
        <span class="balance-mark" aria-hidden="true"></span>
        {{ account.account.usage.remainingTokens === null ? 'Безлимит' : `${account.account.usage.remainingTokens.toLocaleString('ru-RU')} токенов` }}
      </RouterLink>
      <RouterLink class="account-link" to="/profile" :aria-label="`Профиль ${account.account.username}`" :title="account.account.username">
        <span class="avatar" aria-hidden="true">{{ account.account.username.slice(0, 1).toUpperCase() }}</span>
      </RouterLink>
    </div>
  </header>
  <main id="main" tabindex="-1">
    <RouterView />
  </main>
</template>
