<script setup lang="ts">
import { ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import Field from '../../shared/Field.vue'
import ModalShell from '../../shared/ModalShell.vue'
import SelectField from '../../shared/SelectField.vue'

const props = defineProps<{
  classId: number
  schoolId: number
  teacherOptions: { value: string; label: string }[]
}>()

const emit = defineEmits<{ close: []; assigned: [] }>()

const teacherId = ref('')
const subjectName = ref('')
const assigning = ref(false)
const error = ref<string | null>(null)

async function handleAssign() {
  if (!teacherId.value || !subjectName.value.trim()) return
  error.value = null
  assigning.value = true
  try {
    await globalAdminApi.assignTeacherToClass(props.classId, props.schoolId, teacherId.value, subjectName.value.trim())
    emit('assigned')
    emit('close')
  } catch (err) {
    error.value = (err as { message?: string }).message ?? 'Could not assign teacher.'
  } finally {
    assigning.value = false
  }
}
</script>

<template>
  <ModalShell title="Assign teacher" @close="emit('close')">
    <form class="form-row" @submit.prevent="handleAssign">
      <SelectField
        v-model="teacherId"
        label="Teacher"
        placeholder="Select teacher"
        :options="teacherOptions"
        searchable
      />
      <Field v-model="subjectName" label="Subject" placeholder="Math" />
      <button type="submit" class="save-btn" :disabled="assigning || !teacherId || !subjectName.trim()">
        {{ assigning ? 'Assigning…' : 'Assign' }}
      </button>
    </form>
    <p class="hint">If this subject doesn't exist yet for the school, it will be created automatically.</p>
    <p v-if="error" class="error-note">{{ error }}</p>
  </ModalShell>
</template>

<style scoped>
.form-row {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  flex-wrap: wrap;
}

.form-row > * {
  min-width: 180px;
}

.save-btn {
  background: var(--green-br);
  color: var(--white);
  border: none;
  border-radius: var(--r-sm);
  padding: 8px 18px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  height: 42px;
}

.save-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.hint {
  margin: 0;
  font-size: 12px;
  color: var(--muted);
}

.error-note {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
