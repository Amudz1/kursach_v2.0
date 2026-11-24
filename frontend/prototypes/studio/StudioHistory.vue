<script setup lang="ts">
import { computed, ref } from 'vue'
import { Folder, MessageCircle, Palette, Plus, Search } from 'lucide-vue-next'
import type { ExampleThread } from './sample-data'

const props = defineProps<{ threads: readonly ExampleThread[]; activeId?: string; appearanceOpen: boolean }>()
const emit = defineEmits<{ start: []; select: [id: string]; appearance: [] }>()
const search = ref('')
const projectsOpen = ref(false)
const selectedProject = ref<string>()
const groups = ['Закреплённые', 'Сегодня', 'Вчера'] as const
const visibleThreads = computed(() => {
  const term = search.value.trim().toLocaleLowerCase('ru')
  return props.threads.filter((thread) =>
    (!selectedProject.value || thread.project === selectedProject.value)
    && `${thread.title} ${thread.project ?? ''}`.toLocaleLowerCase('ru').includes(term))
})
const projects = computed(() => [...new Set(props.threads.flatMap((thread) => thread.project ? [thread.project] : []))])
</script>

<template>
  <div class="studio-history">
    <button class="studio-wordmark" type="button" aria-label="Budz, первый экран" @click="emit('start')">budz<span>.</span></button>
    <div class="studio-history-tools">
      <button class="studio-new" type="button" @click="emit('start')"><Plus aria-hidden="true" />Новый диалог</button>
      <label class="studio-search">
        <span class="sr-only">Поиск по истории</span>
        <Search aria-hidden="true" />
        <input v-model="search" type="search" placeholder="Найти диалог">
      </label>
    </div>
    <div class="studio-history-list">
      <button v-if="projects.length" class="studio-project-toggle" type="button" :aria-expanded="projectsOpen" @click="projectsOpen = !projectsOpen">
        <Folder aria-hidden="true" />Проекты
      </button>
      <div v-if="projectsOpen" class="studio-projects">
        <button v-for="project in projects" :key="project" class="studio-thread" type="button" :aria-pressed="project === selectedProject"
          @click="selectedProject = selectedProject === project ? undefined : project">{{ project }}</button>
      </div>
      <nav aria-label="История диалогов">
        <template v-for="group in groups" :key="group">
          <section v-if="visibleThreads.some(thread => thread.group === group)" class="studio-history-group">
            <h2>{{ group }}</h2>
            <button v-for="thread in visibleThreads.filter(item => item.group === group)" :key="thread.id" class="studio-thread"
              type="button" :aria-current="thread.id === activeId ? 'page' : undefined" @click="emit('select', thread.id)">
              <MessageCircle aria-hidden="true" />
              <span><strong>{{ thread.title }}</strong><small v-if="thread.project">{{ thread.project }}</small></span>
            </button>
          </section>
        </template>
        <p v-if="!visibleThreads.length" class="studio-history-empty">{{ search ? 'Диалоги не найдены' : 'Список диалогов пуст' }}</p>
      </nav>
    </div>
    <button class="studio-appearance-link" type="button" :aria-current="appearanceOpen ? 'page' : undefined" @click="emit('appearance')">
      <Palette aria-hidden="true" />Оформление
    </button>
  </div>
</template>
