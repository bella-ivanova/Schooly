<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminTeacherSubject } from '../../../api/types'
import SelectField from '../../shared/SelectField.vue'
import ModalShell from '../../shared/ModalShell.vue'

const props = defineProps<{
  teacherId: string
  teacherLabel: string
  subjectOptions: { value: string; label: string }[]
}>()

const emit = defineEmits<{ close: []; changed: [] }>()

const assigned = ref<AdminTeacherSubject[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

const removingId = ref<number | null>(null)
const newSubjectId = ref('')
const assigning = ref(false)
const assignError = ref<string | null>(null)

const availableSubjectOptions = computed(() => {
  const assignedIds = new Set(assigned.value.map((s) => s.subjectId))
  return props.subjectOptions.filter((o) => !assignedIds.has(Number(o.value)))
})

async function loadSubjects() {
  loading.value = true
  loadError.value = null
  try {
    assigned.value = await schoolAdminApi.getTeacherSubjects(props.teacherId)
  } catch (err) {
    loadError.value = (err as { message?: string }).message ?? 'Could not load subjects.'
  } finally {
    loading.value = false
  }
}

async function handleAssign() {
  if (!newSubjectId.value) return
  assignError.value = null
  assigning.value = true
  try {
    await schoolAdminApi.assignSubjectToTeacher(props.teacherId, Number(newSubjectId.value))
    newSubjectId.value = ''
    await loadSubjects()
    emit('changed')
  } catch (err) {
    assignError.value = (err as { message?: string }).message ?? 'Could not assign subject.'
  } finally {
    assigning.value = false
  }
}

async function handleRemove(subjectId: number) {
  removingId.value = subjectId
  try {
    await schoolAdminApi.removeSubjectFromTeacher(props.teacherId, subjectId)
    await loadSubjects()
    emit('changed')
  } finally {
    removingId.value = null
  }
}

onMounted(loadSubjects)
</script>

<template>
  <ModalShell :title="`Subjects — ${teacherLabel}`" @close="emit('close')">
    <div v-if="loading" class="loading">Loading...</div>
    <p v-else-if="loadError" class="error-note">{{ loadError }}</p>
    <template v-else>
      <ul v-if="assigned.length" class="subjects-list">
        <li v-for="s in assigned" :key="s.subjectId" class="subject-row">
          <span>{{ s.subjectName }}</span>
          <button
            class="remove-btn"
            :disabled="removingId === s.subjectId"
            @click="handleRemove(s.subjectId)"
          >
            {{ removingId === s.subjectId ? 'Removing…' : 'Remove' }}
          </button>
        </li>
      </ul>
      <p v-else class="empty-note">No qualified subjects yet.</p>

      <form class="add-row" @submit.prevent="handleAssign">
        <SelectField
          v-model="newSubjectId"
          label="Subject"
          placeholder="Select subject"
          :options="availableSubjectOptions"
        />
        <button type="submit" class="add-btn" :disabled="assigning || !newSubjectId">
          {{ assigning ? 'Adding…' : 'Add' }}
        </button>
      </form>
      <p v-if="assignError" class="error-note">{{ assignError }}</p>
    </template>
  </ModalShell>
</template>

<style scoped>
.loading {
  color: var(--muted);
  font-size: 14px;
}

.subjects-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.subject-row {
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
