<script setup lang="ts">
import { computed, ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminUserSummary, ClassRosterStudent } from '../../../api/types'
import SelectField from '../../shared/SelectField.vue'

const props = defineProps<{
  classId: number
  students: ClassRosterStudent[]
  allUsers: AdminUserSummary[]
}>()

const emit = defineEmits<{ changed: [] }>()

const removingId = ref<string | null>(null)
const newStudentId = ref('')
const adding = ref(false)
const addError = ref<string | null>(null)

const addableStudentOptions = computed(() => {
  const enrolledIds = new Set(props.students.map((s) => s.id))
  return props.allUsers
    .filter((u) => u.role === 'Student' && !enrolledIds.has(u.id))
    .map((u) => ({ value: u.id, label: u.fullName || u.username }))
})

async function handleAdd() {
  if (!newStudentId.value) return
  addError.value = null
  adding.value = true
  try {
    await schoolAdminApi.assignStudent(props.classId, newStudentId.value)
    newStudentId.value = ''
    emit('changed')
  } catch (err) {
    addError.value = (err as { message?: string }).message ?? 'Could not add student.'
  } finally {
    adding.value = false
  }
}

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

    <form class="add-row" @submit.prevent="handleAdd">
      <SelectField
        v-model="newStudentId"
        label="Add student"
        placeholder="Select student"
        :options="addableStudentOptions"
      />
      <button type="submit" class="add-btn" :disabled="adding || !newStudentId">
        {{ adding ? 'Adding…' : 'Add' }}
      </button>
    </form>
    <p v-if="addError" class="error-note">{{ addError }}</p>
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

.add-row {
  display: flex;
  align-items: flex-end;
  gap: 10px;
  flex-wrap: wrap;
}

.add-row > * {
  min-width: 180px;
}

.add-btn {
  background: var(--green-br);
  color: var(--white);
  border: none;
  border-radius: var(--r-sm);
  padding: 8px 16px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  height: 42px;
}

.add-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.error-note {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
