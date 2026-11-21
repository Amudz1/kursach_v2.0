<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, explain } from '../api'
import { entityColor, entityIcon } from '../appearance/entities'
import ProjectEditor from '../components/ProjectEditor.vue'
import UiAvatar from '../components/ui/UiAvatar.vue'
import UiCard from '../components/ui/UiCard.vue'
import type { Workspace } from '../types'

const projects = ref<Workspace[]>([])
const editor = ref<InstanceType<typeof ProjectEditor>>()
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { projects.value = await api<Workspace[]>('/workspaces') }
  catch (reason) { error.value = explain(reason) }
  finally { loading.value = false }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page-heading project-heading">
      <div><h1>Проекты</h1><p>Диалоги, инструкции и материалы для каждой задачи.</p></div>
      <button type="button" class="primary" @click="editor?.show()">Новый проект</button>
    </header>
    <p v-if="error" class="notice danger" role="alert">{{ error }} <button class="text-button" @click="load">Повторить</button></p>
    <p v-else-if="loading" role="status">Загружаем проекты…</p>
    <p v-else-if="!projects.length" class="muted">Проектов пока нет.</p>
    <div v-else class="project-cards">
      <UiCard v-for="project in projects" :key="project.id" as="article" class="stack" interactive :category="entityColor(project.color, project.id)">
        <div class="actions">
          <UiAvatar :name="project.name" :color="entityColor(project.color, project.id)" :icon="entityIcon(project.icon, 'folder')" decorative />
          <h2>{{ project.name }}</h2>
        </div>
        <div class="actions">
          <RouterLink :to="{ path: '/chats', query: { workspace: project.id } }" class="primary">Открыть проект</RouterLink>
          <button type="button" class="secondary" @click="editor?.show(project)">Настройки проекта</button>
          <RouterLink :to="{ path: '/data', query: { workspace: project.id } }" class="text-button">Перенос данных</RouterLink>
        </div>
      </UiCard>
    </div>
    <ProjectEditor ref="editor" @saved="load" @removed="load" />
  </section>
</template>

<style scoped>
.project-heading { display: flex; align-items: end; flex-wrap: wrap; justify-content: space-between; gap: 1rem; }
.project-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 335px), 1fr)); gap: .85rem; }
</style>
