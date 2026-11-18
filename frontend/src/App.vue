<script setup lang="ts">
import { computed, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { useSession } from './session'

const account = useSession()
const route = useRoute()
const router = useRouter()
const fullScreen = computed(() => route.name === 'login' || route.name === 'onboarding')
const nav = [
  { to: '/chats', label: 'Диалоги', glyph: '▤' },
  { to: '/personalization', label: 'Персонализация', glyph: '◉' },
  { to: '/memory', label: 'Память', glyph: '▣' },
  { to: '/plans', label: 'Тарифы', glyph: '◫' },
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
    <RouterLink class="header-balance" to="/plans" aria-label="Доступные токены и тарифы">
      <span aria-hidden="true">◆</span>
      {{ account.account.usage.remainingTokens === null ? 'Безлимит' : `${account.account.usage.remainingTokens.toLocaleString('ru-RU')} токенов` }}
    </RouterLink>
    <RouterLink class="account-link" to="/profile">
      <span class="avatar" aria-hidden="true">{{ account.account.username.slice(0, 1).toUpperCase() }}</span>
      <span class="account-name">{{ account.account.username }}</span>
      <span class="sr-only">Профиль</span>
    </RouterLink>
  </header>
  <main id="main" :class="{ 'workspace-main': route.name === 'chat', 'has-dock': !fullScreen && account.account }" tabindex="-1">
    <RouterView />
  </main>
  <nav v-if="!fullScreen && account.account" class="dock" aria-label="Основная навигация">
    <RouterLink v-for="item in nav" :key="item.to" :to="item.to">
      <span class="dock-glyph" aria-hidden="true">{{ item.glyph }}</span><span>{{ item.label }}</span>
    </RouterLink>
    <RouterLink v-if="account.account.role === 'Admin'" to="/admin" aria-label="Администрирование">
      <span class="dock-glyph" aria-hidden="true">⚙</span><span>Админ</span>
    </RouterLink>
  </nav>
</template>
