<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as chatApi from '../../api/chat'
import type { ApiError, ChatSessionSummary } from '../../api/types'
import ChatSessionRow from './ChatSessionRow.vue'

const props = withDefaults(defineProps<{ basePath?: string }>(), {
  basePath: '/app/student/chat',
})

const emit = defineEmits<{
  'session-updated': []
}>()

const sessions = ref<ChatSessionSummary[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const deletingId = ref<number | null>(null)
const deleteError = ref<string | null>(null)

onMounted(async () => {
  try {
    const result = await chatApi.getSessions()
    sessions.value = [...result].sort(
      (a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime(),
    )
  } catch (err) {
    const apiError = err as ApiError
    loadError.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load your chats.'
  } finally {
    loading.value = false
  }
})

async function handleDelete(id: number) {
  if (deletingId.value) return
  deleteError.value = null
  deletingId.value = id
  try {
    await chatApi.deleteSession(id)
    sessions.value = sessions.value.filter((s) => s.id !== id)
    emit('session-updated')
  } catch (err) {
    const apiError = err as ApiError
    deleteError.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not delete this chat.'
  } finally {
    deletingId.value = null
  }
}
</script>

<template>
  <div class="all-chats-view">
    <h1 class="page-title">All Chats</h1>
    <p v-if="deleteError" class="delete-error">{{ deleteError }}</p>

    <div v-if="loading" class="state-msg">Loading…</div>
    <div v-else-if="loadError" class="state-msg error">{{ loadError }}</div>
    <p v-else-if="sessions.length === 0" class="state-msg">No past chats yet.</p>
    <ul v-else class="session-list">
      <ChatSessionRow
        v-for="session in sessions"
        :key="session.id"
        :session="session"
        :base-path="props.basePath"
        :deleting="deletingId === session.id"
        @delete="handleDelete"
      />
    </ul>
  </div>
</template>

<style scoped>
.all-chats-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
}

.page-title {
  position: sticky;
  /* AppShell's .app-content has padding-top: 40px, which sticky's "top: 0"
     resolves against — that padding strip itself is not covered by the sticky
     box, so scrolled rows can render into it. Offsetting by -40px pulls the
     sticky box up to the true top of the scrollport so nothing renders above it. */
  top: -40px;
  z-index: 1;
  margin: 0;
  padding: 4px 0 12px;
  background: var(--paper);
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
}

.state-msg {
  font-size: 14px;
  color: var(--muted);
}

.state-msg.error {
  color: var(--t-lit);
}

.delete-error {
  margin: 0;
  font-size: 13px;
  color: var(--t-lit);
}

.session-list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
</style>
