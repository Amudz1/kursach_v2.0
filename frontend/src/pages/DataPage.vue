<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, explain } from '../api'
import { Upload } from 'lucide-vue-next'
import UiProgress from '../components/ui/UiProgress.vue'
import { exportConversations } from '../data-transfer'
import type { Workspace } from '../types'

type ImportReport = { imported: number; skipped: number; chatIds: string[] }

const route = useRoute()
const router = useRouter()
const startingWorkspace = typeof route.query.workspace === 'string' ? route.query.workspace : ''
const chatId = typeof route.query.chat === 'string' ? route.query.chat : ''
const spaces = ref<Workspace[]>([])
const target = ref(startingWorkspace)
const exportScope = ref(chatId ? 'chat' : startingWorkspace)
const includeAccount = ref(false)
const file = ref<File>()
const picker = ref<HTMLInputElement>()
const result = ref<ImportReport>()
const importedWorkspace = ref('')
const openingQuery = computed(() => importedWorkspace.value ? { workspace: importedWorkspace.value } : {})
const importing = ref(false)
const fileHover = ref(false)
const downloading = ref(false)
const importError = ref('')
const exportError = ref('')
const listError = ref('')
const temporaryError = ref('')
const startingTemporary = ref(false)

async function load() {
  listError.value = ''
  try {
    spaces.value = await api<Workspace[]>('/workspaces')
    if (target.value && !spaces.value.some((space) => space.id === target.value)) target.value = ''
    if (exportScope.value && exportScope.value !== 'chat' && !spaces.value.some((space) => space.id === exportScope.value)) exportScope.value = ''
  } catch (reason) { listError.value = explain(reason) }
}

function selectFile(event: Event) {
  setImportFile((event.target as HTMLInputElement).files?.[0])
}

function setImportFile(selected?: File) {
  file.value = selected
  importError.value = ''
  result.value = undefined
}

function dropImport(event: DragEvent) {
  fileHover.value = false
  if (importing.value) return
  if (picker.value) picker.value.value = ''
  setImportFile(event.dataTransfer?.files[0])
}

async function importFile() {
  if (!file.value || importing.value) return
  result.value = undefined
  importError.value = ''
  if (file.value.size > 12 * 1024 * 1024) {
    importError.value = 'Выберите JSON размером до 12 МБ.'
    return
  }
  const destination = target.value
  importing.value = true
  try {
    const body = new FormData()
    body.set('file', file.value)
    if (destination) body.set('workspaceId', destination)
    result.value = await api<ImportReport>('/data/import', { method: 'POST', body })
    importedWorkspace.value = destination
    if (picker.value) picker.value.value = ''
    file.value = undefined
  } catch (reason) { importError.value = explain(reason) }
  finally { importing.value = false }
}

async function download(format: 'json' | 'markdown') {
  if (downloading.value) return
  exportError.value = ''
  downloading.value = true
  try {
    await exportConversations(format, {
      chatId: exportScope.value === 'chat' ? chatId : undefined,
      workspaceId: exportScope.value === 'chat' ? undefined : exportScope.value,
      includeAccount: includeAccount.value && !exportScope.value,
    })
  } catch (reason) { exportError.value = explain(reason) }
  finally { downloading.value = false }
}

async function temporaryChat() {
  startingTemporary.value = true
  temporaryError.value = ''
  try {
    const chat = await api<{ id: string }>('/chats', { method: 'POST', body: { temporary: true } })
    await router.push(`/chats/${chat.id}`)
  } catch (reason) { temporaryError.value = explain(reason) }
  finally { startingTemporary.value = false }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading"><h1>Управление данными</h1><p>Перенесите переписку в Budz или скачайте её для другого сервиса.</p></header>
    <p v-if="listError" class="notice danger" role="alert">{{ listError }} <button class="text-button" @click="load">Повторить</button></p>
    <div class="data-grid">
      <form class="fieldset stack" @submit.prevent="importFile">
        <h2>Импорт диалогов</h2>
        <p class="help">Распакуйте выгрузку ChatGPT или Claude и выберите JSON с перепиской. Поддерживаются также JSON-выгрузки Budz.</p>
        <label>Куда импортировать <select v-model="target" aria-label="Куда импортировать" :disabled="importing">
          <option value="">Личные диалоги</option><option v-for="space in spaces" :key="space.id" :value="space.id">{{ space.name }}</option>
        </select></label>
        <label class="drop-zone" :class="{ 'drop-active': fileHover }" @dragover.prevent="fileHover = !importing"
          @dragleave="fileHover = false" @drop.prevent="dropImport">
          <span><Upload aria-hidden="true" />Файл переписки</span>
          <span class="help">{{ file ? file.name : 'Перетащите JSON сюда или выберите файл.' }}</span>
          <input ref="picker" aria-label="Файл переписки" type="file" accept=".json,application/json" :disabled="importing" @change="selectFile">
        </label>
        <UiProgress v-if="importing" :value="0" label="Импорт диалогов" indeterminate />
        <button type="submit" class="primary self-start" :disabled="importing || !file">{{ importing ? 'Импортируем…' : 'Импортировать' }}</button>
        <p v-if="importError" class="notice danger" role="alert">{{ importError }}</p>
        <div v-if="result" class="notice success" role="status">
          <p>Импортировано {{ result.imported }}. Повторов пропущено {{ result.skipped }}.</p>
          <RouterLink v-if="result.chatIds[0]" :to="{ path: `/chats/${result.chatIds[0]}`, query: openingQuery }" class="text-button">Открыть диалог</RouterLink>
        </div>
        <details><summary>Формат для других сервисов</summary>
          <p class="help">Подойдёт JSON с массивом диалогов. Для каждого диалога задайте название <code>title</code> и массив <code>messages</code> с полями автора <code>role</code> (<code>user</code> или <code>assistant</code>) и текста <code>content</code>.</p>
        </details>
      </form>
      <section class="fieldset stack" aria-labelledby="export-title">
        <h2 id="export-title">Экспорт данных</h2>
        <label>Что выгрузить <select v-model="exportScope" aria-label="Что выгрузить" :disabled="downloading">
          <option v-if="chatId" value="chat">Текущий диалог</option>
          <option value="">Все диалоги</option>
          <option v-for="space in spaces" :key="space.id" :value="space.id">{{ space.name }}</option>
        </select></label>
        <label v-if="!exportScope" class="check"><input v-model="includeAccount" type="checkbox" :disabled="downloading"> Добавить память и настройки в JSON</label>
        <div class="actions">
          <button class="primary" type="button" :disabled="downloading" @click="download('json')">Скачать JSON</button>
          <button class="secondary" type="button" :disabled="downloading" @click="download('markdown')">Скачать Markdown</button>
        </div>
        <p class="help">JSON можно вернуть в Budz или приложить к новому чату другого сервиса. Markdown удобно приложить или вставить как текст.</p>
        <p class="help">Переносится текст переписки. Исходные вложения скачиваются из диалогов отдельно. Импорт создаёт диалоги и сохраняет текущие настройки аккаунта.</p>
        <p v-if="exportError" class="notice danger" role="alert">{{ exportError }}</p>
      </section>
    </div>
    <section class="fieldset stack data-privacy" aria-labelledby="temp-title">
      <h2 id="temp-title">Временный диалог</h2>
      <p class="help">Текстовый чат без общей памяти и экспорта. Он доступен только по прямому адресу в вашем аккаунте в течение 24 часов. После этого сервер удаляет его.</p>
      <button class="secondary self-start" type="button" :disabled="startingTemporary" @click="temporaryChat">Открыть временный диалог</button>
      <p v-if="temporaryError" class="notice danger" role="alert">{{ temporaryError }}</p>
    </section>
    <details class="fieldset data-privacy">
      <summary>Что передаётся модели</summary>
      <p>При отправке запроса модель получает текущее сообщение, подходящую часть истории, активные инструкции и релевантный контекст. В проекте используются его материалы, а в личных диалогах может использоваться разрешённая память. Сам импорт не отправляет содержимое файла модели.</p>
    </details>
  </section>
</template>

<style scoped>
.drop-zone { padding: var(--space-4); border: 1px dashed var(--dark-rule); border-radius: var(--radius-md);
  background: var(--surface-2); transition: background-color var(--motion-base), border-color var(--motion-base), box-shadow var(--motion-base); }
.drop-zone > span:first-child { display: flex; align-items: center; gap: var(--space-2); }
.drop-zone svg { width: 18px; height: 18px; color: var(--accent); }
.drop-zone:focus-within, .drop-zone.drop-active { border-color: var(--accent); background: var(--surface-3); box-shadow: var(--glow-sm); }
.data-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); align-items: start; gap: 1rem; }
details > p { padding-top: .55rem; }
.data-privacy { margin-top: 1rem; }
@media (max-width: 760px) { .data-grid { grid-template-columns: 1fr; } }
</style>
