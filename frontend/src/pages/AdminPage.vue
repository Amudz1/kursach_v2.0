<script setup lang="ts">
import { ref, watch } from 'vue'
import { api, explain } from '../api'
import { useSession } from '../session'
import StatisticsPanel from '../components/StatisticsPanel.vue'
import type { AdminPersona, AdminPlan, AdminUser, Page, Statistics } from '../types'

const session = useSession()
const tabs = { stats: 'Статистика', users: 'Пользователи', personas: 'Персоны', plans: 'Тарифы', context: 'Контекст' } as const
const tab = ref<keyof typeof tabs>('stats')
const stats = ref<Statistics>()
const users = ref<AdminUser[]>([])
const personas = ref<AdminPersona[]>([])
const plans = ref<AdminPlan[]>([])
const choices = ref<Record<string, string>>({})
const days = ref(30)
const page = ref(1)
const search = ref('')
const hasMore = ref(false)
const error = ref('')
const success = ref('')
const loading = ref(false)
const saving = ref(false)

const newPersona = (): AdminPersona['settings'] => ({
  profile: { name: '', description: '', instructions: '', temperature: 0.5 }, model: '', isActive: true,
})
const newPlan = (): AdminPlan['settings'] => ({
  code: '', name: '', description: '', price: 0, durationDays: 30, dailyTokenLimit: null,
  maxPersonas: 1, maxMemories: 20, maxAttachmentBytes: 2097152,
  allowedModels: ['deepseek/deepseek-chat'], sortOrder: 10, isActive: true,
})
const personaForm = ref(newPersona())
const planForm = ref(newPlan())
const modelInput = ref('deepseek/deepseek-chat')
const dailyInput = ref('')
const personaId = ref('')
const planId = ref('')
const contextId = ref('')
const context = ref('')
const dialog = ref<HTMLDialogElement>()
const removal = ref<{ collection: 'personas' | 'plans'; id: string; name: string }>()

async function load() {
  const current = tab.value
  error.value = ''; loading.value = true
  try {
    if (current === 'stats') stats.value = await api<Statistics>(`/admin/stats?days=${days.value}`)
    if (current === 'users') {
      const params = new URLSearchParams({ search: search.value, page: String(page.value) })
      const [result, catalog] = await Promise.all([api<Page<AdminUser>>(`/admin/users?${params}`), api<AdminPlan[]>('/admin/plans')])
      users.value = result.items; hasMore.value = result.hasMore; plans.value = catalog
    }
    if (current === 'personas') personas.value = await api<AdminPersona[]>('/admin/personas')
    if (current === 'plans') plans.value = await api<AdminPlan[]>('/admin/plans')
  } catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}
watch(tab, () => { success.value = ''; void load() }, { immediate: true })

async function updateUser(user: AdminUser, body: object) {
  saving.value = true; error.value = ''; success.value = ''
  try { await api(`/admin/users/${user.id}`, { method: 'PATCH', body }); await load(); success.value = 'Аккаунт обновлён.' }
  catch (reason) { error.value = explain(reason) }
  finally { saving.value = false }
}
function editPersona(item?: AdminPersona) {
  personaId.value = item?.id ?? ''
  personaForm.value = item ? { ...item.settings, profile: { ...item.settings.profile } } : newPersona()
}
function editPlan(item?: AdminPlan) {
  planId.value = item?.id ?? ''
  planForm.value = item ? { ...item.settings } : newPlan()
  modelInput.value = planForm.value.allowedModels.join(', ')
  dailyInput.value = planForm.value.dailyTokenLimit === null ? '' : String(planForm.value.dailyTokenLimit)
}
async function saveCatalog(collection: 'personas' | 'plans') {
  const persona = collection === 'personas'
  const id = persona ? personaId.value : planId.value
  const body = persona ? personaForm.value : { ...planForm.value,
    dailyTokenLimit: dailyInput.value.trim() === '' ? null : Number(dailyInput.value),
    allowedModels: modelInput.value.split(',').map((model) => model.trim()).filter(Boolean),
  }
  saving.value = true; success.value = ''; error.value = ''
  try {
    await api(`/admin/${collection}${id ? `/${id}` : ''}`, { method: id ? 'PUT' : 'POST', body })
    persona ? editPersona() : editPlan()
    await load(); success.value = 'Изменения сохранены.'
  } catch (reason) { error.value = explain(reason) }
  finally { saving.value = false }
}
function confirmDelete(collection: 'personas' | 'plans', id: string, name: string) {
  removal.value = { collection, id, name }; dialog.value?.showModal()
}
async function remove() {
  if (!removal.value) return
  try {
    await api(`/admin/${removal.value.collection}/${removal.value.id}`, { method: 'DELETE' })
    if (removal.value.id === personaId.value) editPersona()
    if (removal.value.id === planId.value) editPlan()
    await load(); success.value = 'Запись удалена.'
  } catch (reason) { error.value = explain(reason) }
  finally { dialog.value?.close() }
}
async function readContext() {
  context.value = ''; error.value = ''; loading.value = true
  try {
    if (!/^[0-9a-f-]{36}$/i.test(contextId.value.trim())) { error.value = 'Введите идентификатор сообщения (UUID).'; return }
    const result = await api<unknown>(`/admin/messages/${contextId.value.trim()}/context`)
    context.value = JSON.stringify(result, null, 2)
  } catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}
</script>

<template>
  <section class="page">
    <header class="page-heading"><h1>Администрирование</h1><p>Статистика и настройки системы.</p></header>
    <div class="actions admin-nav" aria-label="Разделы администрирования">
      <button v-for="(label, key) in tabs" :key="key" class="secondary"
        :aria-pressed="tab === key" @click="tab = key">{{ label }}</button>
    </div>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button v-if="tab !== 'context'" class="text-button" @click="load">Повторить</button></p>
    <p v-if="success" class="notice success" role="status">{{ success }}</p>
    <p v-if="loading" class="muted" role="status">Загружаем данные…</p>

    <div v-if="tab === 'stats'" class="stack">
      <label class="period">Период <select v-model.number="days" aria-label="Период" @change="load">
        <option :value="7">7 дней</option><option :value="30">30 дней</option><option :value="90">90 дней</option>
      </select></label>
      <StatisticsPanel v-if="stats" :data="stats" admin />
    </div>

    <div v-if="tab === 'users'" class="stack">
      <form class="actions" @submit.prevent="page = 1; load()"><label class="period">Поиск пользователя <input v-model="search" type="search" maxlength="254"></label><button type="submit" class="primary">Найти</button></form>
      <article v-for="user in users" :key="user.id" class="record account-record">
        <div><h3>{{ user.username }}</h3><p>{{ user.email }}</p><small>{{ user.role === 'Admin' ? 'Администратор' : 'Пользователь' }} · {{ user.isBlocked ? 'Доступ закрыт' : 'Доступ активен' }} · {{ user.planCode }}</small></div>
        <div class="actions">
          <button v-if="user.id !== session.account?.id" class="text-button" :disabled="saving" @click="updateUser(user, { isBlocked: !user.isBlocked })">
            {{ user.isBlocked ? 'Разблокировать' : 'Заблокировать' }}
          </button>
          <select v-model="choices[user.id]" :aria-label="`Новый тариф для ${user.username}`">
            <option disabled value="">Выберите тариф</option><option v-for="item in plans.filter((entry) => entry.settings.isActive)" :key="item.id" :value="item.id">{{ item.settings.code }}</option>
          </select>
          <button class="secondary" :disabled="!choices[user.id] || saving" @click="updateUser(user, { planId: choices[user.id] })">Назначить</button>
        </div>
      </article>
      <p v-if="!loading && !users.length" class="muted">Пользователи не найдены.</p>
      <div class="actions"><button v-if="page > 1" class="secondary" @click="page--; load()">Назад</button>
        <button v-if="hasMore" class="secondary" @click="page++; load()">Дальше</button></div>
    </div>

    <div v-if="tab === 'personas'" class="profile-grid">
      <div class="stack">
        <article v-for="item in personas" :key="item.id" class="record">
          <div><h3>{{ item.settings.profile.name }}</h3><p>{{ item.settings.profile.description }}</p><small>{{ item.settings.isActive ? 'Доступна' : 'Отключена' }}</small></div>
          <div class="actions"><button class="text-button" @click="editPersona(item)">Изменить</button>
            <button class="text-button danger-text" @click="confirmDelete('personas', item.id, item.settings.profile.name)">Удалить</button></div>
        </article>
      </div>
      <form class="fieldset stack" @submit.prevent="saveCatalog('personas')">
        <h2>{{ personaId ? 'Редактировать персону' : 'Новая системная персона' }}</h2>
        <label>Название <input v-model="personaForm.profile.name" maxlength="80" required></label>
        <label>Описание <input v-model="personaForm.profile.description" maxlength="500"></label>
        <label>Инструкции <textarea v-model="personaForm.profile.instructions" rows="4" maxlength="4000" required></textarea></label>
        <label>Рекомендуемая модель <input v-model="personaForm.model" maxlength="160"></label>
        <label>Температура <input v-model.number="personaForm.profile.temperature" type="number" min="0" max="2" step="0.05" required></label>
        <label class="check"><input v-model="personaForm.isActive" type="checkbox"> Доступна пользователям</label>
        <div class="actions"><button type="submit" class="primary" :disabled="saving">Сохранить персону</button>
          <button v-if="personaId" type="button" class="secondary" @click="editPersona()">Отмена</button></div>
      </form>
    </div>

    <div v-if="tab === 'plans'" class="profile-grid">
      <div class="stack">
        <article v-for="item in plans" :key="item.id" class="record">
          <div><h3>{{ item.settings.code }} · {{ item.settings.name }}</h3><p>{{ item.settings.description }}</p><small>{{ item.settings.isActive ? 'Активен' : 'Снят с продажи' }}</small></div>
          <div class="actions"><button class="text-button" @click="editPlan(item)">Изменить</button>
            <button v-if="item.settings.code !== 'Free'" class="text-button danger-text" @click="confirmDelete('plans', item.id, item.settings.code)">Удалить</button></div>
        </article>
      </div>
      <form class="fieldset stack" @submit.prevent="saveCatalog('plans')">
        <h2>{{ planId ? 'Редактировать тариф' : 'Новый тариф' }}</h2>
        <div class="form-grid">
          <label>Код <input v-model="planForm.code" maxlength="20" required :disabled="!!planId"></label>
          <label>Название <input v-model="planForm.name" maxlength="100" required></label>
          <label>Цена, ₽ <input v-model.number="planForm.price" type="number" min="0" step="0.01" required></label>
          <label>Дней <input v-model.number="planForm.durationDays" type="number" min="0" max="3650" required></label>
          <label>Токенов в день <input v-model="dailyInput" type="number" min="0" max="1000000000" placeholder="Безлимит"></label>
          <label>Макс. персон <input v-model.number="planForm.maxPersonas" type="number" min="0" max="1000" required></label>
          <label>Макс. фактов <input v-model.number="planForm.maxMemories" type="number" min="0" max="1000" required></label>
          <label>Вложения, байт <input v-model.number="planForm.maxAttachmentBytes" type="number" min="0" max="16777216" required></label>
          <label>Порядок <input v-model.number="planForm.sortOrder" type="number" required></label>
        </div>
        <label>Описание <textarea v-model="planForm.description" rows="2" maxlength="500"></textarea></label>
        <label>Модели через запятую <input v-model="modelInput" required></label>
        <label class="check"><input v-model="planForm.isActive" type="checkbox" :disabled="planForm.code === 'Free'"> Доступен для подключения</label>
        <div class="actions"><button type="submit" class="primary" :disabled="saving">Сохранить тариф</button>
          <button v-if="planId" type="button" class="secondary" @click="editPlan()">Отмена</button></div>
      </form>
    </div>

    <div v-if="tab === 'context'" class="stack">
      <form class="fieldset stack" @submit.prevent="readContext">
        <label>Идентификатор ответа <input v-model="contextId" maxlength="36" required placeholder="UUID сообщения"></label>
        <button type="submit" class="primary self-start" :disabled="loading">Показать контекст</button>
      </form>
      <pre v-if="context"><code>{{ context }}</code></pre>
    </div>

    <dialog ref="dialog" aria-labelledby="remove-catalog">
      <form class="stack" @submit.prevent="remove"><h2 id="remove-catalog">Удалить запись?</h2><p>{{ removal?.name }}</p>
        <div class="actions"><button type="button" class="secondary" autofocus @click="dialog?.close()">Отмена</button>
          <button type="submit" class="danger-button">Удалить</button></div>
      </form>
    </dialog>
  </section>
</template>

<style scoped>
.record { transition: background-color var(--motion-base), border-color var(--motion-base); }
.record:hover, .record:focus-within { background: var(--surface-2); border-color: var(--accent); }
</style>
