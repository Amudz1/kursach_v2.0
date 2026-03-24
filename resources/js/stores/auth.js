import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import axios from 'axios'

export const useAuthStore = defineStore('auth', () => {
    const user  = ref(JSON.parse(localStorage.getItem('user') || 'null'))
    const token = ref(localStorage.getItem('auth_token') || null)

    const isLoggedIn       = computed(() => !!token.value)
    const hasSubscription  = computed(() => user.value?.has_subscription ?? false)
    const remainingPrompts = computed(() => user.value?.remaining_prompts ?? 12)
    const username         = computed(() => user.value?.username ?? '')

    function setAuth(data) {
        token.value = data.token
        user.value  = data.user
        localStorage.setItem('auth_token', data.token)
        localStorage.setItem('user', JSON.stringify(data.user))
        axios.defaults.headers.common['Authorization'] = `Bearer ${data.token}`
    }

    function updateUser(userData) {
        user.value = { ...user.value, ...userData }
        localStorage.setItem('user', JSON.stringify(user.value))
    }

    async function logout() {
        try { await axios.post('/logout') } catch {}
        token.value = null
        user.value  = null
        localStorage.removeItem('auth_token')
        localStorage.removeItem('user')
        delete axios.defaults.headers.common['Authorization']
    }

    async function fetchMe() {
        try {
            const { data } = await axios.get('/me')
            updateUser(data)
        } catch {
            logout()
        }
    }

    return {
        user, token, isLoggedIn, hasSubscription,
        remainingPrompts, username,
        setAuth, updateUser, logout, fetchMe
    }
})
