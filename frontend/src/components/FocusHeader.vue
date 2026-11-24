<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { BookOpen, Brain, CreditCard, Database, Folder, LogOut, MessagesSquare, Palette, PanelLeft, ShieldCheck, SlidersHorizontal, User } from 'lucide-vue-next'
import { explain } from '../api'
import { focusHistoryTarget } from '../focus-history-target'
import { useSession } from '../session'
import type { Account } from '../types'
import InterfaceMenuItems from './InterfaceMenuItems.vue'
import UiAvatar from './ui/UiAvatar.vue'
import UiDropdown from './ui/UiDropdown.vue'
import UiProgress from './ui/UiProgress.vue'
import { useToast } from './ui/use-toast'

const props = defineProps<{ account: Account }>()
const session = useSession()
const route = useRoute()
const toast = useToast()
const links = [
  { to: '/chats', label: 'Диалоги', icon: MessagesSquare },
  { to: '/workspaces', label: 'Проекты', icon: Folder },
  { to: '/personalization', label: 'Персонализация', icon: SlidersHorizontal },
  { to: '/memory', label: 'Память', icon: Brain },
  { to: '/plans', label: 'Тарифы', icon: CreditCard },
  { to: '/profile', label: 'Профиль', icon: User },
  { to: '/appearance', label: 'Оформление', icon: Palette },
  { to: '/data', label: 'Данные', icon: Database },
  { to: '/help', label: 'Справка', icon: BookOpen },
]
const remaining = computed(() => props.account.usage.remainingTokens)
const limit = computed(() => props.account.usage.dailyTokenLimit)
const balance = computed(() => remaining.value === null ? 'Безлимит' : remaining.value.toLocaleString('ru-RU'))

function bindHistoryTarget(element: unknown) {
  focusHistoryTarget.value = element instanceof HTMLElement ? element : null
}
function active(to: string) {
  if (route.path === to) return 'page'
  if (route.path.startsWith(`${to}/`)) return 'location'
}
async function logout() {
  try { await session.signOut() }
  catch (error) { toast.error(explain(error)) }
}
</script>

<template>
  <header class="topbar focus-topbar">
    <div class="focus-brand">
      <div v-if="route.name === 'chat'" :ref="bindHistoryTarget" class="history-target"></div>
      <RouterLink v-else to="/chats" class="focus-icon" aria-label="История диалогов" title="История диалогов"><PanelLeft aria-hidden="true" /></RouterLink>
      <RouterLink class="brand" to="/chats" aria-label="AI Helper Budz, диалоги"><span aria-hidden="true">budz<span class="focus-dot">.</span></span></RouterLink>
      <span class="focus-name">Фокус</span>
    </div>
    <div class="focus-account">
      <RouterLink class="focus-balance" to="/plans" :aria-label="`Доступные токены и тарифы. ${balance}`">
        <span>{{ balance }}</span>
        <UiProgress v-if="remaining !== null && limit" :value="remaining" :max="limit" label="Остаток дневного бюджета" meter />
      </RouterLink>
      <UiDropdown label="Меню аккаунта">
        <template #trigger="{ attrs }">
          <button v-bind="attrs" type="button" class="focus-icon"><UiAvatar class="avatar" :name="account.username" decorative /></button>
        </template>
        <RouterLink v-for="item in links" :key="item.to" :to="item.to" :aria-current="active(item.to)" role="menuitem" tabindex="-1">
          <component :is="item.icon" aria-hidden="true" />{{ item.label }}
        </RouterLink>
        <RouterLink v-if="account.role === 'Admin'" to="/admin" :aria-current="active('/admin')" role="menuitem" tabindex="-1">
          <ShieldCheck aria-hidden="true" />Администрирование
        </RouterLink>
        <div role="separator"></div>
        <InterfaceMenuItems />
        <div role="separator"></div>
        <button type="button" role="menuitem" tabindex="-1" @click="logout"><LogOut aria-hidden="true" />Выйти</button>
      </UiDropdown>
    </div>
  </header>
</template>

<style scoped>
.focus-topbar { display: flex; justify-content: space-between; gap: var(--space-2); padding-inline: clamp(8px, 1.6vw, 24px);
  background: var(--surface-1); border-color: var(--border); }
.focus-brand, .focus-account { display: flex; align-items: center; min-width: 0; gap: var(--space-2); }
.focus-topbar .brand { font-size: clamp(1.65rem, 3vw, 2.1rem); }
.focus-dot { color: var(--accent); }
.focus-name { padding-inline-start: var(--space-2); border-inline-start: 1px solid var(--border);
  color: var(--text-muted); font-size: var(--font-size-xs); white-space: nowrap; }
.focus-icon { display: grid; place-items: center; width: 44px; height: 44px; padding: 0; color: var(--text-muted);
  background: transparent; border: 1px solid transparent; border-radius: var(--radius-md); }
.focus-icon:hover { color: var(--text); background: var(--surface-2); }
.focus-icon svg { width: 18px; height: 18px; }
.history-target :deep(.chat-control) { width: 44px; height: 44px; }
.focus-balance { display: grid; gap: 3px; justify-items: center; min-height: 44px; align-content: center; padding-inline: 3px; color: var(--text);
  font-variant-numeric: tabular-nums; font-size: var(--font-size-sm); font-weight: 650; white-space: nowrap; text-decoration: none; }
.focus-balance :deep(.ui-progress) { width: 100%; }
.focus-account :deep(.menu) { max-height: calc(100dvh - var(--header) - 10px); overflow: auto; }
.focus-account :deep([aria-current]) { background: var(--surface-2); font-weight: 700; }
@media (max-width: 900px) {
  .focus-account :deep([role="menuitem"]), .focus-account :deep([role="menuitemradio"]) { min-height: 44px; }
}
@media (max-width: 425px) { .focus-brand, .focus-account { gap: 4px; } }
</style>
