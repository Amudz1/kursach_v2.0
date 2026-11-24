import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import { useSession } from './session'
import './appearance'
import './styles/index.css'

const pinia = createPinia()
const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/chats' },
    { path: '/login', name: 'login', component: () => import('./pages/LoginPage.vue') },
    { path: '/onboarding', name: 'onboarding', component: () => import('./pages/OnboardingPage.vue') },
    { path: '/chats/:id?', name: 'chat', component: () => import('./pages/ChatWorkspace.vue') },
    { path: '/workspaces', name: 'workspaces', component: () => import('./pages/WorkspacesPage.vue') },
    { path: '/data', name: 'data', component: () => import('./pages/DataPage.vue') },
    { path: '/personalization', name: 'personalization', component: () => import('./pages/PersonalizationPage.vue') },
    { path: '/memory', name: 'memory', component: () => import('./pages/MemoryPage.vue') },
    { path: '/personas', name: 'personas', component: () => import('./pages/PersonasPage.vue') },
    { path: '/plans', name: 'plans', component: () => import('./pages/PlansPage.vue') },
    { path: '/profile', name: 'profile', component: () => import('./pages/ProfilePage.vue') },
    { path: '/appearance', name: 'appearance', component: () => import('./pages/AppearancePage.vue') },
    { path: '/help', name: 'help', meta: { publicHelp: true }, component: () => import('./pages/HelpPage.vue') },
    { path: '/help/documents', name: 'help-documents', meta: { publicHelp: true }, component: () => import('./pages/HelpPage.vue') },
    { path: '/help/documents/:document(terms|privacy)', name: 'help-document', meta: { publicHelp: true }, component: () => import('./pages/HelpPage.vue') },
    { path: '/help/support', name: 'help-support', meta: { publicHelp: true }, component: () => import('./pages/HelpPage.vue') },
    { path: '/admin', name: 'admin', meta: { admin: true }, component: () => import('./pages/AdminPage.vue') },
    { path: '/admin/support/:userId?', name: 'admin-support', meta: { admin: true }, component: () => import('./pages/AdminSupportPage.vue') },
    // The component showcase exists only in development builds.
    ...(import.meta.env.DEV ? [{ path: '/dev/ui', name: 'dev-ui', component: () => import('./pages/UiShowcasePage.vue') }] : []),
    { path: '/:pathMatch(.*)*', redirect: '/chats' },
  ],
})

router.beforeEach(async (to) => {
  if (to.name === 'dev-ui') return true
  const session = useSession(pinia)
  await session.ready()
  if (to.meta.publicHelp) return true
  if (!session.account) return to.name === 'login' ? true : { name: 'login' }
  if (!session.account.onboardingCompleted) return to.name === 'onboarding' ? true : { name: 'onboarding' }
  if (to.meta.admin && session.account.role !== 'Admin') return { name: 'chat' }
  return to.name === 'login' || to.name === 'onboarding' ? { name: 'chat' } : true
})

createApp(App).use(pinia).use(router).mount('#app')
