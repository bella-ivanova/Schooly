<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminClassDetail, AdminUserSummary } from '../../../api/types'
import Field from '../../shared/Field.vue'
import SelectField from '../../shared/SelectField.vue'
import ClassRosterEditor from './ClassRosterEditor.vue'
import ClassTeacherAssignmentsEditor from './ClassTeacherAssignmentsEditor.vue'

const props = defineProps<{
  classId: number
  allUsers: AdminUserSummary[]
  teacherOptions: { value: string; label: string }[]
  subjectOptions: { value: string; label: string }[]
}>()

const emit = defineEmits<{ close: []; updated: [] }>()

const detail = ref<AdminClassDetail | null>(null)
const loadingDetail = ref(true)
const loadError = ref<string | null>(null)

const nameInput = ref('')
const subjectIdInput = ref('')
const saving = ref(false)
const saveError = ref<string | null>(null)

async function loadDetail() {
  loadingDetail.value = true
  loadError.value = null
  try {
    detail.value = await schoolAdminApi.getClassDetail(props.classId)
    nameInput.value = detail.value.name
    subjectIdInput.value = detail.value.subjectId != null ? String(detail.value.subjectId) : ''
  } catch (err) {
    loadError.value = (err as { message?: string }).message ?? 'Could not load class.'
  } finally {
    loadingDetail.value = false
  }
}

async function refetchAndNotify() {
  await loadDetail()
  emit('updated')
}

async function handleSave() {
  if (!nameInput.value.trim() || !subjectIdInput.value) return
  saveError.value = null
  saving.value = true
  try {
    await schoolAdminApi.updateClass(props.classId, nameInput.value.trim(), Number(subjectIdInput.value))
    await refetchAndNotify()
  } catch (err) {
    saveError.value = (err as { message?: string }).message ?? 'Could not save changes.'
  } finally {
    saving.value = false
  }
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') emit('close')
}

onMounted(() => {
  loadDetail()
  window.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div class="modal-overlay" @click.self="$emit('close')">
    <div class="modal-card">
      <div class="modal-header">
        <h2 class="modal-title">Edit class</h2>
        <button class="close-btn" aria-label="Close" @click="$emit('close')">×</button>
      </div>

      <div v-if="loadingDetail" class="loading">Loading...</div>
      <p v-else-if="loadError" class="error-note">{{ loadError }}</p>
      <div v-else-if="detail" class="modal-body">
        <form class="details-row" @submit.prevent="handleSave">
          <Field v-model="nameInput" label="Class name" maxlength="50" />
          <SelectField
            v-model="subjectIdInput"
            label="Subject"
            placeholder="Select subject"
            :options="subjectOptions"
          />
          <button type="submit" class="save-btn" :disabled="saving || !nameInput.trim() || !subjectIdInput">
            {{ saving ? 'Saving…' : 'Save' }}
          </button>
        </form>
        <p v-if="saveError" class="error-note">{{ saveError }}</p>

        <hr class="divider" />

        <ClassRosterEditor
          :class-id="classId"
          :students="detail.students"
          :all-users="allUsers"
          @changed="refetchAndNotify"
        />

        <hr class="divider" />

        <ClassTeacherAssignmentsEditor
          :class-id="classId"
          :assignments="detail.teacherAssignments"
          :teacher-options="teacherOptions"
          :subject-options="subjectOptions"
          @changed="refetchAndNotify"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(43, 45, 43, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  z-index: 100;
}

.modal-card {
  background: var(--card);
  border-radius: var(--r-lg);
  box-shadow: var(--shadow-lg);
  padding: 24px;
  width: 100%;
  max-width: 560px;
  max-height: calc(100vh - 40px);
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.modal-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 22px;
  color: var(--ink);
}

.close-btn {
  background: none;
  border: none;
  font-size: 22px;
  line-height: 1;
  color: var(--muted);
  cursor: pointer;
  padding: 4px;
}

.loading {
  color: var(--muted);
  font-size: 14px;
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.details-row {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  flex-wrap: wrap;
}

.details-row > * {
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

.divider {
  border: none;
  border-top: 1px solid var(--line);
  margin: 0;
}

.error-note {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
