<script setup lang="ts">
import ChatPage from './ChatPage.vue'
import type { ChatState } from '../use-chat'
import type { LoadVoiceSession, VoiceSession } from '../voice-session'

defineProps<{
  chat: ChatState; voice?: VoiceSession; loadVoice: LoadVoiceSession; confirmVoiceDeparture: () => Promise<boolean>
}>()
</script>

<template>
  <ChatPage :chat="chat" :voice="voice" :load-voice="loadVoice" :confirm-voice-departure="confirmVoiceDeparture"
    focus class="focus-chat" data-interface="focus" />
</template>

<style scoped>
.focus-chat { --focus-start-width: 760px; --focus-composer-radius: 24px; --focus-touch-target: 44px; }
.focus-chat :deep(.is-start :is(.chat-start, .composer-wrap, .chat-start-extra)) { max-width: var(--focus-start-width); }
.focus-chat :deep(.chat-start h1) { overflow-wrap: anywhere; }
.focus-chat :deep(.focus-start-ideas) { gap: var(--space-2); }
.focus-chat :deep(.focus-start-ideas .ui-button) { min-height: var(--focus-touch-target); }
.focus-chat :deep(.focus-start-ideas .ui-button::before) { content: none; }
.focus-chat :deep(.focus-project-actions) {
  display: flex; gap: var(--space-2); flex-wrap: wrap; margin-block-start: var(--space-3);
}
.focus-chat :deep(.focus-project-actions .ui-button) { min-height: var(--focus-touch-target); }
.focus-chat :deep(.is-start .resume-chat) {
  margin-block-start: var(--space-5); padding-inline: 0 var(--space-1);
  background: transparent; border: 0; border-block-start: 1px solid var(--border); border-radius: 0;
}
.focus-chat :deep(.is-start .resume-chat:hover) { background: var(--surface-2); }
.focus-chat :deep(.focus-resume-icon) {
  display: grid; place-items: center; width: 40px; height: 40px;
  color: var(--accent); background: var(--surface-3); border-radius: var(--radius-md);
}
.focus-chat :deep(.focus-resume-icon svg) { width: 16px; color: inherit; }
.focus-chat :deep(.is-start .project-overview) { margin-block-start: var(--space-4); }
.focus-chat :deep(.conversation-header:has(.focus-conversation-heading)) {
  padding-inline: max(var(--chat-gutter), calc((100% - var(--chat-content-width)) / 2));
  padding-block: var(--space-2);
}
.focus-chat :deep(.focus-conversation-heading h1) {
  overflow-wrap: anywhere;
  font: 400 clamp(1.6rem, 3.2vw, 3rem)/1.08 var(--font-serif); letter-spacing: -.035em;
  display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 2; overflow: hidden;
}
.focus-chat :deep(.transcript) { overflow-anchor: none; }
.focus-chat :deep(.transcript:focus-visible) { outline-offset: -2px; }
.focus-chat :deep(.message-more) { display: none; }
@container chat-transcript (max-width: 639px) {
  .focus-chat :deep(.message.user) { --message-copy-size: var(--focus-touch-target); }
  .focus-chat :deep(.message.user .message-meta:has(.message-copy)) {
    min-height: var(--focus-touch-target);
    padding-inline-end: var(--message-copy-size);
  }
  .focus-chat :deep(.message-meta .message-copy) {
    position: absolute; inset-block-start: var(--space-1); inset-inline-end: var(--space-1);
  }
  :global(:root[data-user-side="right"] .focus-chat .message.user .message-meta:has(.message-copy)) {
    padding-inline: var(--message-copy-size) 0;
  }
  :global(:root[data-user-side="right"] .focus-chat .message-meta .message-copy) {
    inset-inline: var(--space-1) auto;
  }
}
@container chat-transcript (max-width: 480px) {
  .focus-chat :deep(.message-more) { display: grid; }
  .focus-chat :deep(.message:not(.actions-expanded) .answer-secondary) { display: none; }
  .focus-chat :deep(.message-actions :is(.message-more, .message-quote, .ui-icon-button)) {
    min-width: var(--focus-touch-target); min-height: var(--focus-touch-target);
  }
}
.focus-chat :deep(.chat-content .composer) {
  grid-template-columns: minmax(0, 1fr);
  padding: var(--space-2);
  border-radius: var(--focus-composer-radius);
}
.focus-chat :deep(.chat-content .composer textarea) {
  grid-area: auto;
  margin: 0;
  padding-inline: var(--space-2);
  max-height: clamp(37px, calc(var(--chat-viewport-height, 100dvh) - var(--header) - 204px), 142px);
}
.focus-chat :deep(.composer-actions) {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}
.focus-chat :deep(.composer-tools) { display: flex; flex-wrap: wrap; gap: var(--space-1); }
.focus-chat :deep(.composer-actions :is(.composer-attach, .composer-action)) {
  min-width: var(--focus-touch-target);
  min-height: var(--focus-touch-target);
  border-radius: var(--radius-full);
}
.focus-chat :deep(.composer-actions .composer-action) {
  display: inline-flex;
  justify-content: center;
  gap: var(--space-1);
}
@media (min-width: 901px) {
  .focus-chat.history-open { grid-template-columns: 256px minmax(0, 1fr); }
}
@media (max-width: 900px) {
  .focus-chat { --focus-composer-radius: 18px; }
  .focus-chat :deep(.is-start .composer-wrap) { margin-block-end: env(safe-area-inset-bottom, 0px); }
  .focus-chat :deep(.is-start .composer-action span) { display: none; }
  .focus-chat :deep(.is-start .composer-actions .composer-action) {
    width: var(--focus-touch-target);
    padding-inline: 0;
  }
  .focus-chat :deep(.mobile-history) {
    inset-block: var(--header) 0;
    width: min(88vw, 280px);
    height: calc(100dvh - var(--header));
  }
}
@media (max-height: 480px) {
  .focus-chat :deep(.chat-start) { padding-block-start: var(--space-3); }
  .focus-chat :deep(.chat-start h1) { margin-block-end: var(--space-3); }
}
</style>
