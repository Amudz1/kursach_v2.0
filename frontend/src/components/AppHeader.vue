<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { Brain, CreditCard, Database, Infinity as Unlimited, LogOut, MessagesSquare, Moon, Palette, ShieldCheck, SlidersHorizontal, Sun, User } from 'lucide-vue-next'
import { appearance, setAppearance } from '../appearance'
import { explain } from '../api'
import { useSession } from '../session'
import type { Account } from '../types'
import AppDrawer from './AppDrawer.vue'
import UiAvatar from './ui/UiAvatar.vue'
import UiDropdown from './ui/UiDropdown.vue'
import UiProgress from './ui/UiProgress.vue'
import UiTooltip from './ui/UiTooltip.vue'
import { useToast } from './ui/use-toast'

const props = defineProps<{ account: Account }>()
const route = useRoute()
const router = useRouter()
const session = useSession()
const toast = useToast()
const drawer = ref(false)
const menuButton = ref<HTMLButtonElement>()
const navigation = ref<HTMLElement>()
const indicator = ref('')
const drawerId = 'app-navigation-drawer'
const active = (to: string) => route.path === to ? 'page' : to === '/chats' && route.name === 'chat' ? 'location' : undefined
const primary = computed(() => [
  { to: '/chats', label: 'Диалоги', icon: MessagesSquare },
  { to: '/personalization', label: 'Персонализация', icon: SlidersHorizontal },
  { to: '/memory', label: 'Память', icon: Brain },
  { to: '/plans', label: 'Тарифы', icon: CreditCard },
  ...(props.account.role === 'Admin' ? [{ to: '/admin', label: 'Админ', icon: ShieldCheck }] : []),
])
const secondary = computed(() => [
  { to: '/profile', label: 'Профиль', icon: User },
  { to: '/appearance', label: 'Оформление', icon: Palette },
  { to: '/data', label: 'Данные', icon: Database },
].filter((item) => item.to !== '/appearance' || router.hasRoute('appearance')))
const systemDark = window.matchMedia('(prefers-color-scheme: dark)')
const darkPreference = ref(systemDark.matches)
const dark = computed(() => appearance.value.mode === 'dark' || (appearance.value.mode === 'system' && darkPreference.value))
const remaining = computed(() => props.account.usage.remainingTokens)
const limit = computed(() => props.account.usage.dailyTokenLimit)
const numberFormat = new Intl.NumberFormat('ru-RU')
const balanceText = computed(() => remaining.value === null ? 'Безлимит' : numberFormat.format(remaining.value))
const balanceDescription = computed(() => remaining.value === null || limit.value === null
  ? 'Дневной бюджет без ограничений'
  : `Осталось ${balanceText.value} из ${numberFormat.format(limit.value)} токенов на сегодня`)

function toggleTheme() {
  setAppearance({ mode: dark.value ? 'light' : 'dark' })
}
async function logout() {
  drawer.value = false
  try { await session.signOut() } catch (error) { toast.error(explain(error)) }
}
function updateIndicator() {
  const item = navigation.value?.querySelector<HTMLElement>('[aria-current]')
  indicator.value = item?.offsetWidth ? `translateX(${item.offsetLeft}px) scaleX(${item.offsetWidth})` : ''
}
function updateSystemMode(event: MediaQueryListEvent) { darkPreference.value = event.matches }
const mobile = window.matchMedia('(max-width: 900px)')
function closeOnDesktop(event: MediaQueryListEvent) { if (!event.matches) drawer.value = false }
let resize: ResizeObserver | undefined
onMounted(() => {
  resize = new ResizeObserver(updateIndicator)
  if (navigation.value) {
    resize.observe(navigation.value)
    for (const item of navigation.value.querySelectorAll('a')) resize.observe(item)
  }
  mobile.addEventListener('change', closeOnDesktop)
  systemDark.addEventListener('change', updateSystemMode)
  updateIndicator()
})
onBeforeUnmount(() => {
  resize?.disconnect()
  mobile.removeEventListener('change', closeOnDesktop)
  systemDark.removeEventListener('change', updateSystemMode)
})
watch(() => route.fullPath, async () => {
  drawer.value = false
  await nextTick()
  updateIndicator()
})
</script>

<template>
  <header class="topbar">
    <div class="header-brand">
      <button ref="menuButton" type="button" class="menu-toggle" aria-label="Открыть меню"
        :aria-expanded="drawer" :aria-controls="drawerId" @click="drawer = !drawer">
        <span class="menu-glyph" aria-hidden="true"><i></i><i></i><i></i></span>
      </button>
      <RouterLink class="brand" to="/chats" aria-label="AI Helper Budz, диалоги">
        <span class="brand-mark" aria-hidden="true">b<span>.</span></span><span>budz<span class="brand-dot">.</span></span>
      </RouterLink>
    </div>
    <nav ref="navigation" class="top-navigation" aria-label="Основная навигация">
      <span class="nav-indicator" :class="{ visible: indicator }" :style="{ transform: indicator }" aria-hidden="true"></span>
      <RouterLink v-for="item in primary" :key="item.to" :to="item.to" :aria-current="active(item.to)">
        <component :is="item.icon" aria-hidden="true" />{{ item.label }}
      </RouterLink>
    </nav>
    <div class="header-account">
      <UiTooltip :text="balanceDescription" placement="bottom" align-end v-slot="{ describedby }">
        <RouterLink class="header-balance" to="/plans" aria-label="Доступные токены и тарифы" :aria-describedby="describedby">
          <span class="balance-value"><Unlimited v-if="remaining === null" aria-hidden="true" />{{ balanceText }}</span>
          <UiProgress v-if="remaining !== null && limit" :value="remaining" :max="limit" label="Остаток дневного бюджета" meter />
        </RouterLink>
      </UiTooltip>
      <UiDropdown label="Меню аккаунта">
        <template #trigger="{ attrs }">
          <button v-bind="attrs" type="button" class="account-link">
            <UiAvatar class="avatar" :name="account.username" decorative />
          </button>
        </template>
        <RouterLink v-for="item in secondary" :key="item.to" :to="item.to" role="menuitem" tabindex="-1">
          <component :is="item.icon" aria-hidden="true" />{{ item.label }}
        </RouterLink>
        <button type="button" role="menuitem" tabindex="-1" @click="toggleTheme">
          <component :is="dark ? Sun : Moon" aria-hidden="true" />{{ dark ? 'Светлая тема' : 'Тёмная тема' }}
        </button>
        <div role="separator"></div>
        <button type="button" role="menuitem" tabindex="-1" @click="logout"><LogOut aria-hidden="true" />Выйти</button>
      </UiDropdown>
    </div>
  </header>
  <AppDrawer :id="drawerId" v-model:open="drawer" label="Меню" :return-focus="menuButton" v-slot="{ close }">
    <div class="drawer-heading">
      <button type="button" class="menu-toggle drawer-close" aria-label="Закрыть меню" @click="close">
        <span class="menu-glyph" aria-hidden="true"><i></i><i></i><i></i></span>
      </button>
      <strong>budz<span class="brand-dot">.</span></strong>
    </div>
    <nav class="drawer-nav" aria-label="Основная навигация">
      <RouterLink v-for="item in primary" :key="item.to" :to="item.to" :aria-current="active(item.to)" @click="close">
        <component :is="item.icon" aria-hidden="true" />{{ item.label }}
      </RouterLink>
    </nav>
    <nav class="drawer-nav drawer-account" aria-label="Аккаунт">
      <RouterLink v-for="item in secondary" :key="item.to" :to="item.to" :aria-current="active(item.to)" @click="close">
        <component :is="item.icon" aria-hidden="true" />{{ item.label }}
      </RouterLink>
      <button type="button" @click="logout"><LogOut aria-hidden="true" />Выйти</button>
    </nav>
  </AppDrawer>
</template>

<style scoped>
.header-brand, .header-account { display: flex; align-items: center; gap: var(--space-2); min-width: 0; }
.topbar .brand-mark { background: linear-gradient(140deg, var(--brand-fill), color-mix(in oklab, var(--brand-fill), var(--btn-primary-bg) 65%)); }
.brand-mark span { display: inline-block; transition: transform var(--motion-base) var(--ease-spring); }
.brand:hover .brand-mark span { transform: rotate(12deg) translateY(-2px); }
.top-navigation { position: relative; isolation: isolate; }
.top-navigation a { display: flex; align-items: center; gap: 6px; padding-inline: clamp(.5rem, 1.1vw, .95rem); }
.top-navigation a[aria-current] { background: transparent; color: var(--on-fill); }
.top-navigation svg, .drawer-nav svg, .balance-value svg { width: 16px; height: 16px; flex: 0 0 auto; }
.nav-indicator { position: absolute; z-index: -1; top: .23rem; bottom: .23rem; left: 0; width: 1px;
  transform-origin: left; opacity: 0; background: var(--contrast-fill);
  transition: transform var(--motion-base) var(--ease-out), opacity var(--motion-fast); }
.nav-indicator.visible { opacity: 1; }
.header-balance { display: grid; gap: 3px; min-width: 48px; min-height: 40px; align-content: center; text-align: center; }
.balance-value { display: inline-flex; justify-content: center; align-items: center; gap: 4px; }
.account-link, .menu-toggle { display: inline-grid; place-items: center; padding: 0; width: 40px; height: 40px; color: var(--text);
  border: 1px solid transparent; background: transparent; border-radius: var(--radius-md); }
.account-link:hover, .menu-toggle:hover { background: var(--surface-2); border-color: var(--border); }
.menu-toggle { display: none; }
.menu-glyph { position: relative; display: block; width: 20px; height: 16px; }
.menu-glyph i { position: absolute; inset: 7px 0 auto; height: 2px; background: currentColor; border-radius: var(--radius-full);
  transition: transform var(--motion-base) var(--ease-out), opacity var(--motion-fast); }
.menu-glyph i:first-child { transform: translateY(-6px); }
.menu-glyph i:last-child { transform: translateY(6px); }
.menu-toggle[aria-expanded="true"] i:first-child, .drawer-close i:first-child { transform: rotate(45deg); }
.menu-toggle[aria-expanded="true"] i:nth-child(2), .drawer-close i:nth-child(2) { opacity: 0; }
.menu-toggle[aria-expanded="true"] i:last-child, .drawer-close i:last-child { transform: rotate(-45deg); }
.drawer-heading { display: flex; align-items: center; gap: var(--space-3); margin-bottom: var(--space-3); font-size: var(--font-size-xl); }
.drawer-close { display: inline-grid; }
.drawer-nav { display: grid; gap: var(--space-1); }
.drawer-account { margin-top: var(--space-3); padding-top: var(--space-3); border-top: 1px solid var(--border); }
.drawer-nav a, .drawer-nav button { display: flex; gap: var(--space-3); align-items: center; min-height: 44px;
  padding: var(--space-2) var(--space-3); border: 1px solid transparent; border-radius: var(--radius-md); background: transparent;
  color: var(--text); text-decoration: none; text-align: left; }
.drawer-nav :is(a, button):hover { background: var(--surface-2); }
.drawer-nav a[aria-current] { background: var(--surface-3); border-color: var(--accent); font-weight: 700; }
@media (max-width: 900px) {
  .topbar { grid-template: auto / minmax(0, 1fr) auto; gap: 4px; padding: 0 8px; }
  .top-navigation { display: none; }
  .menu-toggle { display: inline-grid; flex: 0 0 auto; }
  .header-brand { gap: 4px; }
  .topbar .brand { gap: 4px; font-size: 1.65rem; }
  .topbar .brand-mark { width: 28px; height: 28px; }
}
</style>
