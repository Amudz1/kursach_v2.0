<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { FileText } from 'lucide-vue-next'
</script>

<template>
  <footer class="legal-notice">
    <RouterLink to="/help/documents"><FileText aria-hidden="true" /> Правовая информация</RouterLink>
  </footer>
</template>

<style scoped>
.legal-notice { padding-block-start: var(--space-3); border-top: 1px solid var(--border); }
.legal-notice a { display: inline-flex; align-items: center; gap: var(--space-2); min-height: 40px; padding-inline: var(--space-2);
  border-radius: var(--radius-sm); color: var(--text-muted); background: var(--surface-2); font-size: var(--font-size-sm); }
.legal-notice a:hover { color: var(--text); }
.legal-notice svg { width: 1em; height: 1em; flex: none; }
</style>
