<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api, explain } from '../api'
import { Brain, Plus, Sparkles } from 'lucide-vue-next'
import { useSession } from '../session'
import UiBadge from '../components/ui/UiBadge.vue'
import UiCard from '../components/ui/UiCard.vue'
import UiEmptyState from '../components/ui/UiEmptyState.vue'
import UiSkeleton from '../components/ui/UiSkeleton.vue'
import UiSwitch from '../components/ui/UiSwitch.vue'
import { useToast } from '../components/ui/use-toast'
import type { Memory, Profile } from '../types'

const items = ref<Memory[]>([])
const profile = ref<Profile>()
const error = ref('')
const toast = useToast()
const session = useSession()
const loading = ref(true)
const editing = ref('')
const editorOpen = ref(false)
const contentInput = ref<HTMLTextAreaElement>()
const addButton = ref<HTMLButtonElement>()
const content = ref('')
const pendingDelete = ref<Memory>()
const dialog = ref<HTMLDialogElement>()
const saving = ref(false)
const changing = ref(new Set<string>())
const changingAuto = ref(false)

async function load() {
  loading.value = true; error.value = ''
  try { [items.value, profile.value] = await Promise.all([api<Memory[]>('/me/memories'), api<Profile>('/me/personalization')]) }
  catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}
async function edit(item?: Memory) {
  editing.value = item?.id ?? ''; content.value = item?.content ?? ''; editorOpen.value = true
  await nextTick()
  contentInput.value?.focus()
}
async function cancel() {
  editing.value = ''; content.value = ''; editorOpen.value = false
  await nextTick()
  addButton.value?.focus()
}
async function save() {
  if (!content.value.trim()) return
  error.value = ''; saving.value = true
  try {
    const current = items.value.find((item) => item.id === editing.value)
    await api(current ? `/me/memories/${current.id}` : '/me/memories', {
      method: current ? 'PUT' : 'POST', body: { content: content.value, isEnabled: current?.isEnabled ?? true },
    })
    await cancel(); await load(); toast.success('Факт сохранён.')
  } catch (reason) { error.value = explain(reason) }
  finally { saving.value = false }
}
async function toggle(item: Memory, value: boolean) {
  if (changing.value.has(item.id)) return
  changing.value.add(item.id)
  const previous = item.isEnabled
  item.isEnabled = value
  try {
    await api(`/me/memories/${item.id}`, { method: 'PUT', body: { content: item.content, isEnabled: value } })
  } catch (reason) { item.isEnabled = previous; error.value = explain(reason) }
  finally { changing.value.delete(item.id) }
}
async function memorySetting(value: boolean) {
  if (!profile.value || changingAuto.value) return
  const previous = profile.value
  const next = { ...previous, autoMemoryEnabled: value }
  changingAuto.value = true
  profile.value = next
  try {
    await api('/me/personalization', { method: 'PUT', body: next })
  } catch (reason) { profile.value = previous; error.value = explain(reason) }
  finally { changingAuto.value = false }
}
function confirm(item: Memory) { pendingDelete.value = item; dialog.value?.showModal() }
async function remove() {
  if (!pendingDelete.value) return
  try { await api(`/me/memories/${pendingDelete.value.id}`, { method: 'DELETE' }); dialog.value?.close(); await load() }
  catch (reason) { error.value = explain(reason); dialog.value?.close() }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading actions page-action-heading">
      <div><h1>Память</h1><p>Факты, которые Budz может учитывать в новых диалогах.</p></div>
      <button v-if="profile && !editorOpen" ref="addButton" type="button" class="primary" @click="edit()">Добавить факт</button>
    </header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button v-if="!profile" class="text-button" @click="load">Повторить</button></p>
    <div v-if="loading" role="status"><span class="sr-only">Загружаем память…</span><UiSkeleton :lines="3" /></div>
    <div v-if="profile" class="profile-grid">
      <div class="stack">
        <UiSwitch :model-value="profile.autoMemoryEnabled" :disabled="changingAuto" label="Сохранять факты из диалогов автоматически" @update:model-value="memorySetting" />
        <UiCard v-for="item in items" :key="item.id" as="article" class="record memory-card" interactive
          :class="{ 'recent-fact': item.source === 'auto' && Date.parse(item.createdAt) >= session.startedAt }">
          <div class="fact"><UiBadge :tone="item.source === 'auto' ? 'accent-2' : 'neutral'" :icon="item.source === 'auto' ? Sparkles : Plus">
              {{ item.source === 'auto' ? 'Из диалога' : 'Добавлено вручную' }}
            </UiBadge><p>{{ item.content }}</p></div>
          <div class="actions"><button class="text-button" @click="edit(item)">Изменить</button>
            <button class="text-button danger-text" @click="confirm(item)">Удалить</button></div>
          <UiSwitch :model-value="item.isEnabled" :disabled="changing.has(item.id)" label="Учитывать факт" @update:model-value="toggle(item, $event)" />
        </UiCard>
        <UiEmptyState v-if="!loading && !items.length" :icon="Brain" title="Фактов пока нет." />
      </div>
      <form v-if="editorOpen" class="fieldset stack form-editor" @submit.prevent="save">
        <h2>{{ editing ? 'Редактирование факта' : 'Добавить факт' }}</h2>
        <label>Что запомнить <textarea ref="contentInput" v-model="content" rows="4" maxlength="500" required placeholder="Например, я изучаю C#"></textarea></label>
        <div class="actions"><button class="primary" :disabled="saving" type="submit">Сохранить факт</button>
          <button type="button" class="secondary" @click="cancel">Отмена</button></div>
        <RouterLink class="text-button" to="/personalization">Другие настройки ↗</RouterLink>
      </form>
    </div>
    <dialog ref="dialog" aria-labelledby="delete-memory">
      <form class="stack" @submit.prevent="remove"><h2 id="delete-memory">Удалить факт?</h2>
        <p>Факт будет исключён из контекста будущих ответов.</p>
        <div class="actions"><button type="button" class="secondary" autofocus @click="dialog?.close()">Отмена</button>
          <button class="danger-button" type="submit">Удалить факт</button></div>
      </form>
    </dialog>
  </section>
</template>

<style scoped>
.memory-card { flex-wrap: wrap; }
.fact { display: grid; gap: var(--space-2); justify-items: start; }
.recent-fact { border-color: var(--accent-2); box-shadow: inset 3px 0 var(--accent-2); }
</style>
