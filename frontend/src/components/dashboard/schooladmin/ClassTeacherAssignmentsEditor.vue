<script setup lang="ts">
import { ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { ClassTeacherAssignment } from '../../../api/types'
import SelectField from '../../shared/SelectField.vue'

const props = defineProps<{
  classId: number
  assignments: ClassTeacherAssignment[]
  teacherOptions: { value: string; label: string }[]
  subjectOptions: { value: string; label: string }[]
}>()

const emit = defineEmits<{ changed: [] }>()

const removingKey = ref<string | null>(null)
const newTeacherId = ref('')
const newSubjectId = ref('')
const assigning = ref(false)
const assignError = ref<string | null>(null)

function keyFor(teacherId: string, subjectId: number) {
  return `${teacherId}:${subjectId}`
}

async function handleAssign() {
  if (!newTeacherId.value || !newSubjectId.value) return
  const subject = props.subjectOptions.find((s) => s.value === newSubjectId.value)
  if (!subject) return

  assignError.value = null
  assigning.value = true
  try {
    await schoolAdminApi.assignTeacherToClass(props.classId, newTeacherId.value, subject.label)
    newTeacherId.value = ''
    newSubjectId.value = ''
    emit('changed')
  } catch (err) {
    assignError.value = (err as { message?: string }).message ?? 'Could not assign teacher.'
  } finally {
    assigning.value = false
  }
}

async function handleRemove(teacherId: string, subjectId: number) {
  removingKey.value = keyFor(teacherId, subjectId)
  try {
    await schoolAdminApi.removeTeacherFromClass(props.classId, teacherId, subjectId)
    emit('changed')
  } finally {
    removingKey.value = null
  }
}
</script>

<template>
  <div class="assignments-editor">
    <h3 class="subsection-title">Subject teachers</h3>
    <ul v-if="assignments.length" class="assignments-list">
      <li v-for="a in assignments" :key="keyFor(a.teacherId, a.subjectId)" class="assignment-row">
        <span><strong>{{ a.subjectName }}</strong> — {{ a.teacherUsername }}</span>
        <button
          class="remove-btn"
          :disabled="removingKey === keyFor(a.teacherId, a.subjectId)"
          @click="handleRemove(a.teacherId, a.subjectId)"
        >
          {{ removingKey === keyFor(a.teacherId, a.subjectId) ? 'Removing…' : 'Remove' }}
        </button>
      </li>
    </ul>
    <p v-else class="empty-note">No subject teachers assigned yet.</p>

    <form class="add-row" @submit.prevent="handleAssign">
      <SelectField v-model="newTeacherId" label="Teacher" placeholder="Select teacher" :options="teacherOptions" searchable />
      <SelectField v-model="newSubjectId" label="Subject" placeholder="Select subject" :options="subjectOptions" />
      <button type="submit" class="add-btn" :disabled="assigning || !newTeacherId || !newSubjectId">
        {{ assigning ? 'Assigning…' : 'Assign' }}
      </button>
    </form>
    <p v-if="assignError" class="error-note">{{ assignError }}</p>
  </div>
</template>

<style scoped>
.assignments-editor {
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

.assignments-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.assignment-row {
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
  min-width: 160px;
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
