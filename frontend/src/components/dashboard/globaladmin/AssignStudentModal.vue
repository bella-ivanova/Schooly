<script setup lang="ts">
import { ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import ModalShell from '../../shared/ModalShell.vue'
import SelectField from '../../shared/SelectField.vue'

const props = defineProps<{
  classId: number
  studentOptions: { value: string; label: string }[]
}>()

const emit = defineEmits<{ close: []; assigned: [] }>()

const studentId = ref('')
const assigning = ref(false)
const error = ref<string | null>(null)

async function handleAssign() {
  if (!studentId.value) return
  error.value = null
  assigning.value = true
  try {
    await globalAdminApi.assignStudentToClass(props.classId, studentId.value)
    emit('assigned')
    emit('close')
  } catch (err) {
    error.value = (err as { message?: string }).message ?? 'Could not assign student.'
  } finally {
    assigning.value = false
  }
}
</script>

<template>
  <ModalShell title="Assign student" @close="emit('close')">
    <form class="form-row" @submit.prevent="handleAssign">
      <SelectField
        v-model="studentId"
        label="Student"
        placeholder="Select student"
        :options="studentOptions"
        searchable
      />
      <button type="submit" class="save-btn" :disabled="assigning || !studentId">
        {{ assigning ? 'Assigning…' : 'Assign' }}
      </button>
    </form>
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
  min-width: 200px;
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

.error-note {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
