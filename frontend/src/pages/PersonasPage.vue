<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, explain } from '../api'
import type { Persona, PersonaInput } from '../types'

const fresh = (): PersonaInput => ({ name: '', description: '', instructions: '', temperature: 0.5 })
const items = ref<Persona[]>([])
const form = ref(fresh())
const editing = ref('')
const error = ref('')
const status = ref('')
const loading = ref(true)
const saving = ref(false)
const target = ref<Persona>()
const dialog = ref<HTMLDialogElement>()
async function load() {
  loading.value = true; error.value = ''
  try { items.value = await api<Persona[]>('/personas') }
  catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}
function start(item?: Persona) {
  editing.value = item?.id ?? ''
  form.value = item ? { name: item.name, description: item.description, instructions: item.instructions, temperature: item.temperature } : fresh()
  status.value = ''
}
async function save() {
  saving.value = true; error.value = ''; status.value = ''
  try {
    await api(editing.value ? `/personas/${editing.value}` : '/personas', { method: editing.value ? 'PUT' : 'POST', body: form.value })
    start(); await load(); status.value = 'Персона сохранена. Её можно выбрать в диалоге.'
  } catch (reason) { error.value = explain(reason) }
  finally { saving.value = false }
}
function confirm(persona: Persona) { target.value = persona; dialog.value?.showModal() }
async function remove() {
  if (!target.value) return
  try {
    await api(`/personas/${target.value.id}`, { method: 'DELETE' })
    if (editing.value === target.value.id) start()
    await load()
  } catch (reason) { error.value = explain(reason) }
  finally { dialog.value?.close() }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading"><h1>Персоны</h1><p>Выберите готовую роль ассистента или опишите свою.</p></header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button class="text-button" @click="load">Обновить список</button></p>
    <p v-if="status" class="notice success" role="status">{{ status }}</p>
    <p v-if="loading" class="muted" role="status">Загружаем персоны…</p>
    <div class="profile-grid">
      <div class="stack">
        <article v-for="persona in items" :key="persona.id" class="record">
          <div><small>{{ persona.isCustom ? 'Ваша персона' : 'Системная персона' }}</small><h3>{{ persona.name }}</h3><p>{{ persona.description }}</p>
            <details><summary>Инструкции</summary><p>{{ persona.instructions }}</p></details>
          </div>
          <div v-if="persona.isCustom" class="actions"><button class="text-button" @click="start(persona)">Изменить</button>
            <button class="text-button danger-text" @click="confirm(persona)">Удалить</button></div>
        </article>
        <p v-if="!loading && !items.length" class="muted">Пока нет доступных персон.</p>
      </div>
      <form class="fieldset stack" @submit.prevent="save">
        <h2>{{ editing ? 'Редактировать персону' : 'Новая персона' }}</h2>
        <label>Название <input v-model="form.name" maxlength="80" required></label>
        <label>Описание <input v-model="form.description" maxlength="500"></label>
        <label>Инструкции <textarea v-model="form.instructions" maxlength="4000" rows="5" required></textarea></label>
        <label>Температура <input v-model.number="form.temperature" type="number" min="0" max="2" step="0.05" required></label>
        <div class="actions"><button type="submit" class="primary" :disabled="saving">Сохранить персону</button>
          <button v-if="editing" type="button" class="secondary" @click="start()">Отмена</button></div>
      </form>
    </div>
    <dialog ref="dialog" aria-labelledby="delete-persona">
      <form class="stack" @submit.prevent="remove"><h2 id="delete-persona">Удалить персону?</h2>
        <p>Диалоги этой персоны будут использовать системный режим.</p>
        <div class="actions"><button type="button" class="secondary" autofocus @click="dialog?.close()">Отмена</button>
          <button class="danger-button" type="submit">Удалить персону</button></div>
      </form>
    </dialog>
  </section>
</template>
