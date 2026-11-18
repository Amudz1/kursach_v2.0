<script setup lang="ts">
import { ref } from 'vue'
import { api, explain } from '../api'
import type { Workspace } from '../types'

const emit = defineEmits<{ saved: [id: string, created: boolean]; removed: [id: string] }>()
const draft = ref<Workspace>({ id: '', name: '', instructions: '', materials: '' })
const dialog = ref<HTMLDialogElement>()
const confirming = ref(false)
const reading = ref(false)
const busy = ref(false)
const error = ref('')

function show(project?: Workspace) {
  draft.value = project ? { ...project } : { id: '', name: '', instructions: '', materials: '' }
  confirming.value = false
  error.value = ''
  dialog.value?.showModal()
}

async function save() {
  if (busy.value || reading.value || !draft.value.name.trim()) return
  busy.value = true
  error.value = ''
  try {
    const { id, ...body } = draft.value
    if (id) await api(`/workspaces/${id}`, { method: 'PUT', body })
    const saved = id || (await api<{ id: string }>('/workspaces', { method: 'POST', body })).id
    dialog.value?.close()
    emit('saved', saved, !id)
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}

async function attachMaterial(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  error.value = ''
  reading.value = true
  try {
    if (!/\.(txt|md)$/i.test(file.name) || file.size > 40003) throw new Error()
    const text = new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer())
    if (text.length > 10000 || text.includes('\0')) throw new Error()
    draft.value.materials = text
  } catch { error.value = 'Выберите TXT или Markdown в UTF-8 объёмом до 10 000 символов.' }
  finally { reading.value = false }
}

async function remove() {
  const { id } = draft.value
  if (!id || busy.value) return
  busy.value = true
  error.value = ''
  try {
    await api(`/workspaces/${id}`, { method: 'DELETE' })
    dialog.value?.close()
    emit('removed', id)
  } catch (reason) { error.value = explain(reason) }
  finally { busy.value = false }
}

defineExpose({ show })
</script>

<template>
  <dialog ref="dialog" class="project-editor" aria-labelledby="project-editor-title" @cancel.prevent="!busy && !reading && dialog?.close()">
    <form class="stack" @submit.prevent="confirming ? remove() : save()">
      <h2 id="project-editor-title">{{ confirming ? 'Удалить проект?' : draft.id ? 'Настройки проекта' : 'Новый проект' }}</h2>
      <p v-if="confirming">Диалоги из проекта останутся в общей истории. Инструкции и материалы проекта будут удалены.</p>
      <template v-else>
        <label>Название <input v-model="draft.name" required maxlength="80" placeholder="Диплом"></label>
        <label>Инструкции проекта <textarea v-model="draft.instructions" rows="4" maxlength="2000"
          placeholder="Например, предлагай решения на C# и объясняй, как их проверить"></textarea></label>
        <label>Материалы проекта <textarea v-model="draft.materials" rows="5" maxlength="10000"
          placeholder="Вставьте текст, который Budz сможет учитывать в диалогах проекта"></textarea></label>
        <label class="help">Заменить материалы из TXT или Markdown <input type="file" accept=".txt,.md" :disabled="reading || busy" @change="attachMaterial"></label>
      </template>
      <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
      <div class="actions">
        <button v-if="draft.id && !confirming" class="text-button danger-text project-remove" type="button" :disabled="busy || reading"
          @click="confirming = true; error = ''">Удалить проект</button>
        <button class="secondary" type="button" :disabled="busy || reading"
          @click="confirming ? (confirming = false, error = '') : dialog?.close()">{{ confirming ? 'Назад' : 'Отмена' }}</button>
        <button v-if="confirming" class="danger-button" type="submit" :disabled="busy">Удалить проект</button>
        <button v-else class="primary" type="submit" :disabled="busy || reading || !draft.name.trim()">Сохранить проект</button>
      </div>
    </form>
  </dialog>
</template>

<style scoped>
.project-editor { width: min(calc(100% - 2rem), 620px); }
.project-remove { margin-right: auto; }
</style>
