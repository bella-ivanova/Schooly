<script setup lang="ts">
import { ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { ClassRosterStudent } from '../../../api/types'

const props = defineProps<{
  classId: number
  students: ClassRosterStudent[]
}>()

const emit = defineEmits<{ changed: [] }>()

const removingId = ref<string | null>(null)

async function handleRemove(userId: string) {
  removingId.value = userId
  try {
    await schoolAdminApi.removeStudent(props.classId, userId)
    emit('changed')
  } finally {
    removingId.value = null
  }
}
</script>

<template>
  <div class="roster-editor">
    <h3 class="subsection-title">Students</h3>
    <ul v-if="students.length" class="roster-list">
      <li v-for="student in students" :key="student.id" class="roster-row">
        <span>{{ student.fullName || student.username }}</span>
        <button
          class="remove-btn"
          :disabled="removingId === student.id"
          @click="handleRemove(student.id)"
        >
          {{ removingId === student.id ? 'Removing…' : 'Remove' }}
        </button>
      </li>
    </ul>
    <p v-else class="empty-note">No students in this class yet.</p>
  </div>
</template>

<style scoped>
.roster-editor {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.subsection-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 15px;
  color: var(--ink);
}

.roster-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.roster-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 8px 12px;
  background: var(--cream-2);
  border-radius: var(--r-sm);
  font-size: 14px;
  color: var(--ink-2);
}

.remove-btn {
  background: none;
  border: 1px solid var(--line-2);
  border-radius: var(--r-sm);
  padding: 4px 10px;
  font-size: 12px;
  color: var(--ink-2);
  cursor: pointer;
}

.remove-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.empty-note {
  margin: 0;
  font-size: 13px;
  color: var(--muted);
}
</style>
