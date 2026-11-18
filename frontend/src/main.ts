import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import { useSession } from './session'
import './theme'
import './style.css'

const pinia = createPinia()
const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/chats' },
    { path: '/login', name: 'login', component: () => import('./pages/LoginPage.vue') },
    { path: '/onboarding', name: 'onboarding', component: () => import('./pages/OnboardingPage.vue') },
    { path: '/chats/:id?', name: 'chat', component: () => import('./pages/ChatPage.vue') },
    { path: '/personalization', name: 'personalization', component: () => import('./pages/PersonalizationPage.vue') },
    { path: '/memory', name: 'memory', component: () => import('./pages/MemoryPage.vue') },
    { path: '/personas', name: 'personas', component: () => import('./pages/PersonasPage.vue') },
    { path: '/plans', name: 'plans', component: () => import('./pages/PlansPage.vue') },
    { path: '/profile', name: 'profile', component: () => import('./pages/ProfilePage.vue') },
    { path: '/admin', name: 'admin', component: () => import('./pages/AdminPage.vue') },
    { path: '/:pathMatch(.*)*', redirect: '/chats' },
  ],
})

router.beforeEach(async (to) => {
  const session = useSession(pinia)
  await session.ready()
  if (!session.account) return to.name === 'login' ? true : { name: 'login' }
  if (!session.account.onboardingCompleted) return to.name === 'onboarding' ? true : { name: 'onboarding' }
  if (to.name === 'admin' && session.account.role !== 'Admin') return { name: 'chat' }
  return to.name === 'login' || to.name === 'onboarding' ? { name: 'chat' } : true
})

createApp(App).use(pinia).use(router).mount('#app')
