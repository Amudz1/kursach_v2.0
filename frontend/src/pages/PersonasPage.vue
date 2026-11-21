<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import { api, explain } from '../api'
import { entityColor, entityIcon } from '../appearance/entities'
import EntityAppearanceFields from '../components/EntityAppearanceFields.vue'
import UiAvatar from '../components/ui/UiAvatar.vue'
import UiCard from '../components/ui/UiCard.vue'
import type { Persona, PersonaInput } from '../types'

const fresh = (): PersonaInput => ({ name: '', description: '', instructions: '', temperature: 0.5 })
const items = ref<Persona[]>([])
const form = ref(fresh())
const editing = ref('')
const editorOpen = ref(false)
const nameInput = ref<HTMLInputElement>()
const addButton = ref<HTMLButtonElement>()
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
async function start(item?: Persona) {
  editing.value = item?.id ?? ''
  form.value = item ? { name: item.name, description: item.description, instructions: item.instructions, temperature: item.temperature,
    color: item.color, icon: item.icon } : fresh()
  status.value = ''
  editorOpen.value = true
  await nextTick()
  nameInput.value?.focus()
}
async function closeEditor() {
  editorOpen.value = false; editing.value = ''; form.value = fresh()
  await nextTick()
  addButton.value?.focus()
}
async function save() {
  saving.value = true; error.value = ''; status.value = ''
  try {
    await api(editing.value ? `/personas/${editing.value}` : '/personas', { method: editing.value ? 'PUT' : 'POST', body: form.value })
    await closeEditor(); await load(); status.value = 'Персона сохранена. Её можно выбрать в диалоге.'
  } catch (reason) { error.value = explain(reason) }
  finally { saving.value = false }
}
function confirm(persona: Persona) { target.value = persona; dialog.value?.showModal() }
async function remove() {
  if (!target.value) return
  try {
    await api(`/personas/${target.value.id}`, { method: 'DELETE' })
    if (editing.value === target.value.id) await closeEditor()
    await load()
  } catch (reason) { error.value = explain(reason) }
  finally { dialog.value?.close() }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading actions page-action-heading">
      <div><h1>Персоны</h1><p>Выберите готовую роль ассистента или опишите свою.</p></div>
      <button v-if="!editorOpen" ref="addButton" type="button" class="primary" @click="start()">Новая персона</button>
    </header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button class="text-button" @click="load">Обновить список</button></p>
    <p v-if="status" class="notice success" role="status">{{ status }}</p>
    <p v-if="loading" class="muted" role="status">Загружаем персоны…</p>
    <div class="profile-grid">
      <div class="stack">
        <UiCard v-for="persona in items" :key="persona.id" as="article" class="record" interactive spotlight :category="entityColor(persona.color, persona.id)">
          <div><div class="actions"><UiAvatar :name="persona.name" :icon="entityIcon(persona.icon)" :color="entityColor(persona.color, persona.id)" decorative />
              <div><small>{{ persona.isCustom ? 'Ваша персона' : 'Системная персона' }}</small><h3>{{ persona.name }}</h3></div></div>
            <p>{{ persona.description }}</p>
            <details><summary>Инструкции</summary><p>{{ persona.instructions }}</p></details>
          </div>
          <div v-if="persona.isCustom" class="actions"><button class="text-button" @click="start(persona)">Изменить</button>
            <button class="text-button danger-text" @click="confirm(persona)">Удалить</button></div>
        </UiCard>
        <p v-if="!loading && !items.length" class="muted">Пока нет доступных персон.</p>
      </div>
      <form v-if="editorOpen" class="fieldset stack form-editor" @submit.prevent="save">
        <h2>{{ editing ? 'Редактировать персону' : 'Новая персона' }}</h2>
        <label>Название <input ref="nameInput" v-model="form.name" maxlength="80" required></label>
        <label>Описание <input v-model="form.description" maxlength="500"></label>
        <label>Инструкции <textarea v-model="form.instructions" maxlength="4000" rows="5" required></textarea></label>
        <label>Температура <input v-model.number="form.temperature" type="number" min="0" max="2" step="0.05" required></label>
        <EntityAppearanceFields v-model:color="form.color" v-model:icon="form.icon" :name="form.name"
          :seed="editing || form.name" fallback-icon="sparkles" />
        <div class="actions"><button type="submit" class="primary" :disabled="saving">Сохранить персону</button>
          <button type="button" class="secondary" @click="closeEditor">Отмена</button></div>
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
