<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminSubjectSummary } from '../../../api/types'
import Field from '../../shared/Field.vue'
import ConfirmModal from '../../shared/ConfirmModal.vue'

const subjects = ref<AdminSubjectSummary[]>([])
const loading = ref(true)

const newSubjectName = ref('')
const creatingSubject = ref(false)
const createSubjectError = ref<string | null>(null)

const deletingSubject = ref<AdminSubjectSummary | null>(null)
const deleting = ref(false)
const deleteError = ref<string | null>(null)

onMounted(async () => {
  subjects.value = await schoolAdminApi.getSubjects()
  loading.value = false
})

async function handleCreateSubject() {
  if (!newSubjectName.value.trim()) return
  createSubjectError.value = null
  creatingSubject.value = true
  try {
    await schoolAdminApi.createSubject(newSubjectName.value.trim())
    subjects.value = await schoolAdminApi.getSubjects()
    newSubjectName.value = ''
  } catch (err) {
    createSubjectError.value = (err as { message?: string }).message ?? 'Could not create subject.'
  } finally {
    creatingSubject.value = false
  }
}

async function handleDeleteSubject() {
  if (!deletingSubject.value) return
  deleteError.value = null
  deleting.value = true
  try {
    await schoolAdminApi.deleteSubject(deletingSubject.value.id)
    subjects.value = await schoolAdminApi.getSubjects()
    deletingSubject.value = null
  } catch (err) {
    deleteError.value = (err as { message?: string }).message ?? 'Could not delete subject.'
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="school-admin-subjects">
    <h1 class="page-title">Subjects</h1>

    <div v-if="loading" class="loading">Loading...</div>
    <template v-else>
      <div class="table-card">
        <table class="subjects-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="subject in subjects" :key="subject.id">
              <td class="subject-name">{{ subject.name }}</td>
              <td class="actions">
                <button class="link-btn danger" @click="deletingSubject = subject">Delete</button>
              </td>
            </tr>
            <tr v-if="subjects.length === 0">
              <td colspan="2" class="empty">No subjects yet.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-if="deleteError" class="delete-error">{{ deleteError }}</p>

      <div class="table-card code-card">
        <div class="code-card-header">
          <h2 class="section-title">Create a subject</h2>
          <p class="section-subtitle">Add a subject before creating a class tagged to it.</p>
        </div>
        <form class="create-row" @submit.prevent="handleCreateSubject">
          <Field v-model="newSubjectName" label="Subject name" placeholder="Math" />
          <button type="submit" class="regen-btn create-btn" :disabled="creatingSubject || !newSubjectName.trim()">
            {{ creatingSubject ? 'Creating…' : 'Add subject' }}
          </button>
        </form>
        <p v-if="createSubjectError" class="create-error">{{ createSubjectError }}</p>
      </div>
    </template>

    <ConfirmModal
      v-if="deletingSubject"
      title="Delete subject"
      :message="`Delete subject '${deletingSubject.name}'? It must not be assigned to any class or teacher.`"
      :loading="deleting"
      @close="deletingSubject = null"
      @confirm="handleDeleteSubject"
    />
  </div>
</template>

<style scoped>
.school-admin-subjects {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
  position: sticky;
  top: -40px;
  z-index: 2;
  padding: 4px 0 12px;
  background: var(--paper);
}

.loading {
  color: var(--muted);
  font-size: 14px;
}

.table-card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  overflow: hidden;
}

.subjects-table {
  width: 100%;
  border-collapse: collapse;
}

.subjects-table th {
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--muted);
  background: var(--cream-2);
  padding: 14px 20px;
}

.subjects-table td {
  padding: 14px 20px;
  font-size: 14px;
  color: var(--ink-2);
  border-top: 1px solid var(--line);
}

.subject-name {
  font-weight: 600;
  color: var(--ink);
}

.actions {
  color: var(--green-deep);
  font-size: 13px;
}

.link-btn {
  background: none;
  border: none;
  color: var(--green-deep);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
}

.link-btn.danger {
  color: var(--t-lit);
}

.empty {
  text-align: center;
  color: var(--muted);
}

.delete-error {
  margin: 0;
  font-size: 13px;
  color: var(--t-lit);
}

.code-card {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.code-card-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.section-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 18px;
  color: var(--ink);
}

.section-subtitle {
  margin: 0;
  font-size: 13px;
  color: var(--muted);
}

.create-row {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  flex-wrap: wrap;
}

.create-row > * {
  min-width: 200px;
}

.create-btn {
  height: 42px;
}

.create-error {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}

.regen-btn {
  background: var(--green-br);
  color: var(--white);
  border: none;
  border-radius: var(--r);
  padding: 8px 18px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.regen-btn:disabled {
  opacity: 0.6;
  cursor: default;
}
</style>
