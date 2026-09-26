<script setup lang="ts">
import { computed } from 'vue'
import { renderMarkdown } from '../../utils/markdown'
import StereometryViewer from './StereometryViewer.vue'

const props = defineProps<{
  role: 'user' | 'assistant'
  content: string
  subject?: string | null
  topic?: string | null
  scene?: string | null
  scenePending?: boolean
  sceneRequested?: boolean
  streaming?: boolean
  statusText?: string | null
  practiceQuestions?: string[] | null
  loadingPracticeQuestions?: boolean
}>()

defineEmits<{
  'request-practice-questions': []
}>()

const tag = computed(() => [props.subject, props.topic].filter(Boolean).join(' · '))
const renderedContent = computed(() => renderMarkdown(props.content))
</script>

<template>
  <div class="bubble-row" :class="role">
    <div v-if="role === 'user'" class="bubble user-bubble">{{ content }}</div>

    <div v-else class="bubble assistant-card">
      <p v-if="tag" class="tag">{{ tag }}</p>
      <div class="body" v-html="renderedContent" />
      <p v-if="streaming && !content" class="typing">{{ statusText ?? '•••' }}</p>

      <StereometryViewer v-if="scene" :scene="scene" />
      <p v-else-if="scenePending && content" class="scene-note pending">Building 3D model…</p>
      <p v-else-if="sceneRequested && !streaming" class="scene-note">Couldn't build a 3D model for this question.</p>

      <template v-if="!streaming">
        <button
          v-if="!practiceQuestions"
          type="button"
          class="practice-btn"
          :disabled="loadingPracticeQuestions"
          @click="$emit('request-practice-questions')"
        >
          {{ loadingPracticeQuestions ? 'Loading…' : 'Get practice questions' }}
        </button>
        <template v-else-if="practiceQuestions.length > 0">
          <hr class="divider" />
          <p class="practice-label">Practice questions</p>
          <div class="practice-list">
            <p v-for="(q, i) in practiceQuestions" :key="i" class="practice-item">{{ i + 1 }}. {{ q }}</p>
          </div>
        </template>
      </template>
    </div>
  </div>
</template>

<style scoped>
.bubble-row {
  display: flex;
  margin-bottom: 16px;
  /* once per message on mount — token updates don't remount the row */
  animation: rise-in 220ms var(--ease-out);
}

.bubble-row.user {
  justify-content: flex-end;
}

.bubble-row.assistant {
  justify-content: flex-start;
}

.bubble {
  max-width: 70%;
  border-radius: var(--r);
}

.user-bubble {
  background: var(--green-deep);
  color: var(--white);
  padding: 12px 16px;
  font-size: 15px;
}

.assistant-card {
  max-width: 78%;
  background: var(--card);
  border: 1px solid var(--line);
  padding: 18px 20px;
  box-shadow: var(--shadow-sm);
}

.tag {
  margin: 0 0 8px;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--green-deep);
}

.body {
  font-size: 15px;
  line-height: 1.55;
  color: var(--ink);
}

.body :deep(p) {
  margin: 0 0 10px;
}

.body :deep(p:last-child) {
  margin-bottom: 0;
}

.body :deep(ul),
.body :deep(ol) {
  margin: 0 0 10px;
  padding-left: 22px;
}

.body :deep(code) {
  background: var(--cream-2);
  border-radius: 4px;
  padding: 1px 5px;
  font-size: 0.9em;
}

.body :deep(pre) {
  background: var(--cream-2);
  border-radius: var(--r-sm);
  padding: 12px 14px;
  overflow-x: auto;
}

.typing {
  margin: 4px 0 0;
  letter-spacing: 3px;
  color: var(--muted);
  animation: breathe 1.4s ease-in-out infinite;
}

.scene-note.pending {
  animation: breathe 1.6s ease-in-out infinite;
}

@keyframes breathe {
  50% {
    opacity: 0.45;
  }
}

.scene-note {
  margin: 12px 0 0;
  font-size: 13px;
  color: var(--muted);
}

.practice-btn {
  margin-top: 14px;
  border: none;
  border-radius: var(--r-sm);
  background: var(--green-deep);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 13px;
  font-weight: 600;
  padding: 9px 14px;
  cursor: pointer;
}

@media (hover: hover) and (pointer: fine) {
  .practice-btn:hover:not(:disabled) {
    background: var(--green);
  }
}

.practice-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.divider {
  margin: 16px 0 12px;
  border: none;
  border-top: 1px solid var(--line);
}

.practice-label {
  margin: 0 0 8px;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--muted);
}

.practice-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.practice-item {
  animation: rise-in 240ms var(--ease-out) backwards;
  margin: 0;
  padding: 10px 12px;
  border-radius: var(--r-sm);
  background: var(--cream-2);
  font-size: 14px;
  color: var(--ink);
}

.practice-item:nth-child(2) {
  animation-delay: 50ms;
}

.practice-item:nth-child(3) {
  animation-delay: 100ms;
}

.body :deep(.katex-display) {
  overflow-x: auto;
  overflow-y: hidden;
}

@media (max-width: 860px) {
  .user-bubble {
    max-width: 88%;
    padding: 10px 14px;
  }

  .assistant-card {
    max-width: 100%;
    width: 100%;
    padding: 14px 16px;
  }
}

@media (pointer: coarse) {
  .practice-btn {
    min-height: var(--tap);
    padding: 10px 16px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .typing,
  .scene-note.pending {
    animation: none;
  }
}
</style>
