<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { BookOpen, FileText, MessagesSquare } from 'lucide-vue-next'
import { useSession } from '../session'
import { helpTopics, legalDocuments } from '../help/content'
import SupportConversation from '../components/SupportConversation.vue'
import UiBadge from '../components/ui/UiBadge.vue'
import UiButton from '../components/ui/UiButton.vue'

const route = useRoute()
const session = useSession()
const links = [
  { name: 'help', title: 'Возможности', to: '/help', icon: BookOpen },
  { name: 'help-documents', title: 'Документы', to: '/help/documents', icon: FileText },
  { name: 'help-support', title: 'Поддержка', to: '/help/support', icon: MessagesSquare },
]
const section = computed(() => route.name === 'help-document' ? 'help-documents' : route.name)
const document = computed(() => {
  const id = route.params.document
  return id === 'terms' || id === 'privacy' ? legalDocuments[id] : undefined
})
</script>

<template>
  <section class="page help-page">
    <header class="page-heading">
      <div><h1>Справка</h1><p>Как пользоваться Budz и связаться с поддержкой.</p></div>
      <RouterLink v-if="!session.account" to="/login" class="text-button">На страницу входа</RouterLink>
    </header>
    <nav class="help-navigation" aria-label="Разделы справки">
      <RouterLink v-for="link in links" :key="link.name" :to="link.to" :aria-current="section === link.name ? 'page' : undefined">
        <component :is="link.icon" aria-hidden="true" />{{ link.title }}
      </RouterLink>
    </nav>

    <div v-if="section === 'help'" class="stack">
      <h2>Возможности Budz</h2>
      <nav class="help-quick-links" aria-label="Быстрая помощь">
        <a v-for="topic in helpTopics.filter(item => item.id)" :key="topic.id" :href="`#${topic.id}`" class="text-button">{{ topic.title }}</a>
      </nav>
      <div class="help-topics">
        <section v-for="topic in helpTopics" :id="topic.id" :key="topic.title" class="fieldset stack" :tabindex="topic.id ? -1 : undefined">
          <h3>{{ topic.title }}</h3>
          <p>{{ topic.text }}</p>
          <ul v-if="topic.steps" class="help-steps">
            <li v-for="step in topic.steps" :key="step">{{ step }}</li>
          </ul>
          <div v-if="topic.links || topic.sources" class="actions">
            <RouterLink v-for="link in topic.links" :key="link.to" :to="link.to" class="text-button">{{ link.label }}</RouterLink>
            <a v-for="source in topic.sources" :key="source.href" :href="source.href" class="text-button">{{ source.label }}</a>
          </div>
        </section>
      </div>
    </div>
    <div v-else-if="section === 'help-documents'" class="stack help-reading">
      <article v-if="document" class="stack">
        <RouterLink to="/help/documents" class="text-button self-start">Все документы</RouterLink>
        <h2>{{ document.title }}</h2>
        <div><UiBadge tone="warning">Демонстрационный шаблон</UiBadge></div>
        <section v-for="entry in document.sections" :key="entry.title" class="stack document-section">
          <h3>{{ entry.title }}</h3><p>{{ entry.text }}</p>
        </section>
      </article>
      <template v-else>
        <h2>Правовая информация</h2>
        <ul class="document-list">
          <li v-for="(item, key) in legalDocuments" :key="key" class="record">
            <RouterLink :to="{ name: 'help-document', params: { document: key } }">{{ item.title }}</RouterLink>
            <UiBadge tone="warning">Демонстрационный шаблон</UiBadge>
          </li>
        </ul>
      </template>
    </div>
    <div v-else class="stack help-reading">
      <h2>Чат поддержки</h2>
      <p>Расскажите, с чем нужна помощь. Сообщение получит сотрудник поддержки Budz.</p>
      <SupportConversation v-if="session.account" :key="session.account.id" endpoint="/me/support/messages" />
      <UiButton v-else class="self-start" to="/login?redirect=support">Войти, чтобы написать</UiButton>
    </div>
  </section>
</template>

<style scoped>
.help-page { padding-block-end: var(--space-6); }
.help-navigation { display: flex; flex-wrap: wrap; gap: var(--space-1); margin-bottom: var(--space-5); border-bottom: 1px solid var(--border); }
.help-navigation a { display: inline-flex; align-items: center; gap: var(--space-1); min-height: 44px; padding-inline: var(--space-2);
  border-bottom: 2px solid transparent; color: var(--text-muted); text-decoration: none; }
.help-navigation a:hover, .help-navigation a[aria-current] { color: var(--text); background: var(--surface-2); }
.help-navigation a[aria-current] { border-color: var(--accent); font-weight: 700; }
.help-navigation svg { width: 1rem; height: 1rem; }
.help-topics { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 19rem), 1fr)); gap: var(--space-3); }
.help-topics p, .document-section p { line-height: 1.65; }
.help-topics section { scroll-margin-block-start: calc(var(--header) + var(--space-2)); }
.help-quick-links { display: flex; flex-wrap: wrap; gap: var(--space-1) var(--space-3); }
.help-quick-links a { display: inline-flex; align-items: center; min-height: 44px; }
.help-steps { display: grid; gap: var(--space-1); padding-inline-start: var(--space-5); line-height: 1.55; }
.help-reading { max-width: 44rem; overflow-wrap: anywhere; }
.document-list { list-style: none; padding: 0; display: grid; gap: var(--space-2); }
.document-list li { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: var(--space-2); }
.document-section { padding-block: var(--space-2); }
</style>
