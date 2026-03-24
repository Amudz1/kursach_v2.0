import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'

export const useChatStore = defineStore('chat', () => {
    const chats          = ref([])
    const activeChatId   = ref(null)
    const messages       = ref([])
    const loading        = ref(false)
    const sending        = ref(false)

    async function loadChats() {
        const { data } = await axios.get('/chats')
        chats.value = data
    }

    async function createChat() {
        const { data } = await axios.post('/chats')
        chats.value.unshift(data)
        return data
    }

    async function loadMessages(chatId) {
        loading.value = true
        activeChatId.value = chatId
        try {
            const { data } = await axios.get(`/chats/${chatId}/messages`)
            messages.value = data
        } finally {
            loading.value = false
        }
    }

    async function sendMessage(chatId, content, file = null) {
        sending.value = true
        try {
            const form = new FormData()
            if (content) form.append('content', content)
            if (file)    form.append('attachment', file)

            const { data } = await axios.post(`/chats/${chatId}/messages`, form, {
                headers: { 'Content-Type': 'multipart/form-data' }
            })

            messages.value.push(data.user_message)
            messages.value.push(data.ai_message)

            // Обновляем счётчик в списке чатов
            const chat = chats.value.find(c => c.id === chatId)
            if (chat) {
                chat.message_count = (chat.message_count || 0) + 2
                chat.last_message_at = new Date().toISOString()
                // Обновляем заголовок если первое сообщение
                if (chat.message_count <= 2) chat.title = data.user_message.content.slice(0, 40)
            }

            return data
        } finally {
            sending.value = false
        }
    }

    async function deleteChat(chatId) {
        await axios.delete(`/chats/${chatId}`)
        chats.value = chats.value.filter(c => c.id !== chatId)
        if (activeChatId.value === chatId) {
            activeChatId.value = null
            messages.value = []
        }
    }

    return {
        chats, activeChatId, messages, loading, sending,
        loadChats, createChat, loadMessages, sendMessage, deleteChat
    }
})
