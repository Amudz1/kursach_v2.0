<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { api, explain } from '../api'
import SupportConversation from '../components/SupportConversation.vue'
import UiButton from '../components/ui/UiButton.vue'
import { supportTime, type SupportQueue, type SupportThread } from '../help/support'

const route = useRoute()
const threads = ref<SupportThread[]>([])
const previous = ref<number | null>(null)
const loading = ref(false)
const error = ref('')
const selected = computed(() => typeof route.params.userId === 'string' ? route.params.userId : '')
const username = computed(() => threads.value.find(thread => thread.userId === selected.value)?.username)
const life = new AbortController()

async function load(older = false) {
  if (loading.value) return
  loading.value = true
  error.value = ''
  try {
    const query = older ? `?before=${previous.value}` : ''
    const queue = await api<SupportQueue>('/admin/support' + query, { signal: life.signal })
    threads.value = older ? [...threads.value, ...queue.threads] : queue.threads
    previous.value = queue.previous
  } catch (reason) {
    if (!life.signal.aborted) error.value = explain(reason)
  } finally { loading.value = false }
}

onMounted(() => void load())
onUnmounted(() => life.abort())
</script>

<template>
  <section class="page">
    <header class="page-heading">
      <div><h1>Поддержка</h1><p>Переписка с пользователями Budz.</p></div>
      <RouterLink to="/admin" class="text-button">Администрирование</RouterLink>
    </header>
    <div class="support-workspace">
      <section class="fieldset stack queue" aria-labelledby="support-queue-title">
        <h2 id="support-queue-title">Обращения</h2>
        <UiButton variant="secondary" size="sm" class="self-start" :loading="loading" @click="load()">Обновить список</UiButton>
        <p v-if="error" class="notice danger" role="alert">{{ error }}</p>
        <p v-else-if="!loading && !threads.length" class="muted">Обращений пока нет.</p>
        <nav v-if="threads.length" class="stack" aria-label="Переписки поддержки">
          <RouterLink v-for="thread in threads" :key="thread.userId" class="thread" :to="{ name: 'admin-support', params: { userId: thread.userId } }">
            <strong>{{ thread.username }}</strong>
            <time :datetime="thread.lastMessageAt">{{ supportTime.format(new Date(thread.lastMessageAt)) }}</time>
          </RouterLink>
        </nav>
        <UiButton v-if="previous !== null" variant="secondary" size="sm" :disabled="loading" @click="load(true)">Ранние обращения</UiButton>
      </section>
      <section v-if="selected" class="stack support-thread">
        <h2>{{ username ? `Переписка с ${username}` : 'Переписка' }}</h2>
        <SupportConversation :key="selected" :endpoint="`/admin/support/${encodeURIComponent(selected)}/messages`" :username="username" staff />
      </section>
      <p v-else class="muted">Выберите обращение, чтобы ответить.</p>
    </div>
  </section>
</template>

<style scoped>
.support-workspace { display: grid; align-items: start; grid-template-columns: minmax(0, .85fr) minmax(0, 2fr); gap: var(--space-3); }
.queue, .support-thread { min-width: 0; overflow-wrap: anywhere; }
.thread { display: grid; gap: var(--space-1); min-height: 44px; padding: var(--space-2); color: var(--text);
  text-decoration: none; border: 1px solid var(--border); border-radius: var(--radius-sm); }
.thread:hover, .thread[aria-current] { background: var(--surface-2); border-color: var(--accent); }
.thread time { font-size: var(--font-size-sm); color: var(--text-muted); }
@media (max-width: 720px) { .support-workspace { grid-template-columns: minmax(0, 1fr); } }
</style>
