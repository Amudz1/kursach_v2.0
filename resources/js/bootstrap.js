import axios from 'axios'

// ── Единая настройка axios для всего приложения ──────────────
axios.defaults.baseURL = '/api'
axios.defaults.headers.common['Accept'] = 'application/json'
axios.defaults.headers.common['X-Requested-With'] = 'XMLHttpRequest'

// Восстанавливаем токен из localStorage при (пере)загрузке страницы
const savedToken = localStorage.getItem('auth_token')
if (savedToken) {
    axios.defaults.headers.common['Authorization'] = `Bearer ${savedToken}`
}

// ── Глобальный обработчик ответов ────────────────────────────
// При 401 — токен протух или недействителен: чистим хранилище и
// перекидываем на /login, чтобы не спамить бесконечными ошибками
axios.interceptors.response.use(
    response => response,
    error => {
        if (error.response?.status === 401) {
            const isAuthRoute = ['/api/login', '/api/register'].some(u =>
                error.config?.url?.includes(u)
            )
            if (!isAuthRoute) {
                // Чистим данные сессии
                localStorage.removeItem('auth_token')
                localStorage.removeItem('user')
                delete axios.defaults.headers.common['Authorization']
                // Перенаправляем на логин (только если ещё не там)
                if (!window.location.pathname.includes('/login')) {
                    window.location.href = '/login'
                }
            }
        }
        return Promise.reject(error)
    }
)

// window.axios указывает на тот же экземпляр
window.axios = axios
