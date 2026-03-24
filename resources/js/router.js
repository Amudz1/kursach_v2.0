import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from './stores/auth'

const routes = [
    {
        path: '/login',
        name: 'login',
        component: () => import('./pages/LoginPage.vue'),
        meta: { guest: true }
    },
    {
        path: '/',
        name: 'home',
        component: () => import('./pages/HomePage.vue'),
        meta: { auth: true }
    },
    {
        path: '/chat',
        name: 'chat',
        component: () => import('./pages/ChatPage.vue'),
        meta: { auth: true }
    },
    {
        path: '/chat/:id',
        name: 'chat-detail',
        component: () => import('./pages/ChatPage.vue'),
        meta: { auth: true }
    },
    {
        path: '/subscription',
        name: 'subscription',
        component: () => import('./pages/SubscriptionPage.vue'),
        meta: { auth: true }
    },
    {
        path: '/profile',
        name: 'profile',
        component: () => import('./pages/ProfilePage.vue'),
        meta: { auth: true }
    },
    {
        path: '/:pathMatch(.*)*',
        redirect: '/'
    }
]

const router = createRouter({
    history: createWebHistory(),
    routes
})

router.beforeEach((to, from, next) => {
    const auth = useAuthStore()
    if (to.meta.auth && !auth.isLoggedIn) return next('/login')
    if (to.meta.guest && auth.isLoggedIn)  return next('/')
    next()
})

export default router
