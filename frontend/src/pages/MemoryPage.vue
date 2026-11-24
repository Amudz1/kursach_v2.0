<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api, explain } from '../api'
import type { Memory, Profile } from '../types'

const items = ref<Memory[]>([])
const profile = ref<Profile>()
const error = ref('')
const notice = ref('')
const loading = ref(true)
const editing = ref('')
const content = ref('')
const pendingDelete = ref<Memory>()
const dialog = ref<HTMLDialogElement>()
const saving = ref(false)

async function load() {
  loading.value = true; error.value = ''
  try { [items.value, profile.value] = await Promise.all([api<Memory[]>('/me/memories'), api<Profile>('/me/personalization')]) }
  catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}
function edit(item: Memory) { editing.value = item.id; content.value = item.content }
function cancel() { editing.value = ''; content.value = '' }
async function save() {
  if (!content.value.trim()) return
  error.value = ''; notice.value = ''; saving.value = true
  try {
    const current = items.value.find((item) => item.id === editing.value)
    await api(current ? `/me/memories/${current.id}` : '/me/memories', {
      method: current ? 'PUT' : 'POST', body: { content: content.value, isEnabled: current?.isEnabled ?? true },
    })
    cancel(); await load(); notice.value = 'Факт сохранён.'
  } catch (reason) { error.value = explain(reason) }
  finally { saving.value = false }
}
async function toggle(item: Memory) {
  try {
    await api(`/me/memories/${item.id}`, { method: 'PUT', body: { content: item.content, isEnabled: !item.isEnabled } })
    item.isEnabled = !item.isEnabled
  } catch (reason) { error.value = explain(reason) }
}
async function memorySetting(event: Event) {
  if (!profile.value) return
  const input = event.target as HTMLInputElement
  try {
    const next = { ...profile.value, autoMemoryEnabled: input.checked }
    await api('/me/personalization', { method: 'PUT', body: next })
    profile.value = next
  } catch (reason) { input.checked = profile.value.autoMemoryEnabled; error.value = explain(reason) }
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
    <header class="page-heading"><h1>Память</h1><p>Факты, которые Budz может учитывать в новых диалогах.</p></header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button v-if="!profile" class="text-button" @click="load">Повторить</button></p>
    <p v-if="notice" class="notice success" role="status">{{ notice }}</p>
    <p v-if="loading" class="muted" role="status">Загружаем память…</p>
    <div v-if="profile" class="profile-grid">
      <div class="stack">
        <label class="check memory-setting"><input type="checkbox" :checked="profile.autoMemoryEnabled" @change="memorySetting"> Сохранять факты из диалогов автоматически</label>
        <article v-for="item in items" :key="item.id" class="record">
          <div><small>{{ item.source === 'auto' ? 'Из диалога' : 'Добавлено вручную' }} · {{ item.isEnabled ? 'Учитывается' : 'Отключён' }}</small><p>{{ item.content }}</p></div>
          <div class="actions"><button class="text-button" @click="edit(item)">Изменить</button>
            <button class="text-button" @click="toggle(item)">{{ item.isEnabled ? 'Отключить' : 'Включить' }}</button>
            <button class="text-button danger-text" @click="confirm(item)">Удалить</button></div>
        </article>
        <p v-if="!loading && !items.length" class="muted">Фактов пока нет.</p>
      </div>
      <form class="fieldset stack" @submit.prevent="save">
        <h2>{{ editing ? 'Редактирование факта' : 'Добавить факт' }}</h2>
        <label>Что запомнить <textarea v-model="content" rows="4" maxlength="500" required placeholder="Например, я изучаю C#"></textarea></label>
        <div class="actions"><button class="primary" :disabled="saving" type="submit">Сохранить факт</button>
          <button v-if="editing" type="button" class="secondary" @click="cancel">Отмена</button></div>
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
