<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, explain } from '../api'
import type { Workspace } from '../types'

const workspaces = ref<Workspace[]>([])
const draft = ref<Workspace>({ id: '', name: '', instructions: '', materials: '' })
const editor = ref<HTMLDialogElement>()
const confirmation = ref<HTMLDialogElement>()
const deleting = ref<Workspace>()
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const dialogError = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { workspaces.value = await api<Workspace[]>('/workspaces') }
  catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}

function edit(space?: Workspace) {
  draft.value = space ? { ...space } : { id: '', name: '', instructions: '', materials: '' }
  dialogError.value = ''
  editor.value?.showModal()
}

async function save() {
  if (busy.value) return
  busy.value = true
  dialogError.value = ''
  try {
    const { id, ...body } = draft.value
    await api(id ? `/workspaces/${id}` : '/workspaces', { method: id ? 'PUT' : 'POST', body })
    editor.value?.close()
    await load()
  } catch (reason) { dialogError.value = explain(reason) }
  finally { busy.value = false }
}

async function attachMaterial(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  dialogError.value = ''
  try {
    if (!/\.(txt|md)$/i.test(file.name) || file.size > 40003) throw new Error()
    const text = new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer())
    if (text.length > 10000 || text.includes('\0')) throw new Error()
    draft.value.materials = text
  } catch { dialogError.value = 'Выберите TXT или Markdown в UTF-8 объёмом до 10 000 символов.' }
}

function confirmRemove(space: Workspace) {
  deleting.value = space
  dialogError.value = ''
  confirmation.value?.showModal()
}

async function remove() {
  if (!deleting.value || busy.value) return
  busy.value = true
  dialogError.value = ''
  try {
    await api(`/workspaces/${deleting.value.id}`, { method: 'DELETE' })
    confirmation.value?.close()
    await load()
  } catch (reason) { dialogError.value = explain(reason) }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading space-heading">
      <div><h1>Рабочие пространства</h1><p>Свои диалоги, инструкции и материалы для каждой задачи.</p></div>
      <button type="button" class="primary" @click="edit()">Создать пространство</button>
    </header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button class="text-button" @click="load">Повторить</button></p>
    <p v-else-if="loading" role="status">Загружаем пространства…</p>
    <p v-else-if="!workspaces.length" class="muted">Пространств пока нет.</p>
    <div v-else class="space-list">
      <article v-for="space in workspaces" :key="space.id" class="fieldset stack">
        <h2>{{ space.name }}</h2>
        <p class="help">{{ space.instructions ? 'Есть инструкции' : 'Без инструкций' }} · {{ space.materials ? 'Есть материалы' : 'Без материалов' }}</p>
        <div class="actions">
          <RouterLink :to="{ path: '/chats', query: { workspace: space.id } }" class="primary">Открыть диалоги</RouterLink>
          <button type="button" class="secondary" @click="edit(space)">Изменить</button>
          <RouterLink :to="{ path: '/data', query: { workspace: space.id } }" class="text-button">Перенос данных</RouterLink>
          <button type="button" class="text-button danger-text" @click="confirmRemove(space)">Удалить</button>
        </div>
      </article>
    </div>
    <dialog ref="editor" class="space-dialog" aria-labelledby="edit-space" @cancel.prevent="!busy && editor?.close()">
      <form class="stack" @submit.prevent="save">
        <h2 id="edit-space">{{ draft.id ? 'Изменить пространство' : 'Новое пространство' }}</h2>
        <label>Название <input v-model="draft.name" required maxlength="80" placeholder="Диплом"></label>
        <label>Инструкции пространства <textarea v-model="draft.instructions" rows="4" maxlength="2000"
          placeholder="Например, предлагай решения на C# и объясняй, как их проверить"></textarea></label>
        <label>Материалы пространства <textarea v-model="draft.materials" rows="5" maxlength="10000"
          placeholder="Вставьте текст, который Budz сможет учитывать в диалогах пространства"></textarea></label>
        <label class="help">Заменить материалы из TXT или Markdown <input type="file" accept=".txt,.md" @change="attachMaterial"></label>
        <p v-if="dialogError" class="notice danger" role="alert">{{ dialogError }}</p>
        <div class="actions"><button class="secondary" type="button" :disabled="busy" @click="editor?.close()">Отмена</button>
          <button class="primary" type="submit" :disabled="busy || !draft.name.trim()">Сохранить пространство</button></div>
      </form>
    </dialog>
    <dialog ref="confirmation" aria-labelledby="remove-space" @cancel.prevent="!busy && confirmation?.close()">
      <form class="stack" @submit.prevent="remove">
        <h2 id="remove-space">Удалить пространство?</h2>
        <p>Диалоги перейдут в личные. Инструкции и материалы пространства будут удалены.</p>
        <p v-if="dialogError" class="notice danger" role="alert">{{ dialogError }}</p>
        <div class="actions"><button class="secondary" type="button" autofocus :disabled="busy" @click="confirmation?.close()">Отмена</button>
          <button class="danger-button" type="submit" :disabled="busy">Удалить пространство</button></div>
      </form>
    </dialog>
  </section>
</template>

<style scoped>
.space-heading { display: flex; align-items: end; flex-wrap: wrap; justify-content: space-between; gap: 1rem; }
.space-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 335px), 1fr)); gap: .85rem; }
.space-dialog { width: min(calc(100% - 2rem), 620px); }
</style>
