import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import axios from 'axios'
import './bootstrap'

// Axios defaults
axios.defaults.baseURL = '/api'
axios.defaults.headers.common['Accept'] = 'application/json'
axios.defaults.headers.common['X-Requested-With'] = 'XMLHttpRequest'

// Sanctum token из localStorage
const token = localStorage.getItem('auth_token')
if (token) axios.defaults.headers.common['Authorization'] = `Bearer ${token}`

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
