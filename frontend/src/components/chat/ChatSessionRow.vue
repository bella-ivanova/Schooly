<script setup lang="ts">
import { onUnmounted, ref } from 'vue'
import type { ChatSessionSummary } from '../../api/types'
import TrashIcon from '../shared/TrashIcon.vue'

const props = defineProps<{
  session: ChatSessionSummary
  basePath: string
  deleting?: boolean
}>()

const emit = defineEmits<{
  delete: [id: number]
}>()

const confirming = ref(false)
let confirmTimeout: ReturnType<typeof setTimeout> | null = null

function handleClick() {
  if (props.deleting) return

  if (!confirming.value) {
    confirming.value = true
    confirmTimeout = setTimeout(() => {
      confirming.value = false
    }, 4000)
    return
  }

  if (confirmTimeout) clearTimeout(confirmTimeout)
  confirming.value = false
  emit('delete', props.session.id)
}

onUnmounted(() => {
  if (confirmTimeout) clearTimeout(confirmTimeout)
})

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <li class="session-row">
    <router-link :to="`${basePath}/${session.id}`" class="row-main">
      <span class="row-title">{{ session.title ?? 'Untitled chat' }}</span>
      <span class="row-meta">
        <span v-if="session.subject" class="subject-pill">{{ session.subject }}</span>
        <span v-if="session.className" class="class-label">{{ session.className }}</span>
        <span class="row-date">{{ formatDate(session.lastMessageAt) }}</span>
      </span>
    </router-link>
    <button
      type="button"
      class="delete-icon-btn"
      :class="{ confirming }"
      :disabled="deleting"
      :title="confirming ? 'Click again to delete' : 'Delete chat'"
      :aria-label="confirming ? 'Click again to delete' : 'Delete chat'"
      @click="handleClick"
    >
      <TrashIcon :size="16" />
    </button>
  </li>
</template>

<style scoped>
.session-row {
  display: flex;
  align-items: stretch;
  border-radius: var(--r-sm);
  background: var(--card);
  border: 1px solid var(--line);
  overflow: hidden;
}

.session-row:hover {
  border-color: var(--green-br);
}

.row-main {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 16px;
  border-right: 1px solid var(--line);
  text-decoration: none;
  color: var(--ink);
}

.row-title {
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row-meta {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  color: var(--muted);
}

.subject-pill {
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--green-deep);
  background: var(--sage-soft);
  border-radius: 999px;
  padding: 3px 10px;
}

.class-label {
  white-space: nowrap;
}

.row-date {
  white-space: nowrap;
}

.delete-icon-btn {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  border: none;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
}

.delete-icon-btn:hover {
  background: var(--cream-2);
  color: var(--t-lit);
}

.delete-icon-btn.confirming {
  background: var(--t-lit);
  color: var(--white);
}

.delete-icon-btn:disabled {
  opacity: 0.6;
  cursor: default;
}
</style>
