<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import type { AdminSubjectSummary, ApiError, SchoolSummary } from '../../../api/types'
import Field from '../../shared/Field.vue'
import SelectField from '../../shared/SelectField.vue'
import ConfirmModal from '../../shared/ConfirmModal.vue'

const subjects = ref<AdminSubjectSummary[]>([])
const schools = ref<SchoolSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const newSubjectSchoolId = ref('')
const newSubjectName = ref('')
const creatingSubject = ref(false)
const createSubjectError = ref<string | null>(null)

const deletingSubject = ref<AdminSubjectSummary | null>(null)
const deleting = ref(false)

const subjectSchoolFilter = ref('')

const schoolOptions = computed(() => schools.value.map((s) => ({ value: String(s.id), label: s.name })))

const schoolFilterOptions = computed(() => [{ value: '', label: 'All schools' }, ...schoolOptions.value])

const filteredSubjects = computed(() =>
  subjectSchoolFilter.value
    ? subjects.value.filter((s) => String(s.schoolId) === subjectSchoolFilter.value)
    : subjects.value,
)

onMounted(async () => {
  loading.value = true
  error.value = null
  try {
    const [subjectsRes, schoolsRes] = await Promise.all([globalAdminApi.getSubjects(), globalAdminApi.getSchools()])
    subjects.value = subjectsRes
    schools.value = schoolsRes
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load subjects.'
  } finally {
    loading.value = false
  }
})

async function handleCreateSubject() {
  if (!newSubjectSchoolId.value || !newSubjectName.value.trim()) return
  createSubjectError.value = null
  creatingSubject.value = true
  try {
    await globalAdminApi.createSubject(Number(newSubjectSchoolId.value), newSubjectName.value.trim())
    subjects.value = await globalAdminApi.getSubjects()
    newSubjectName.value = ''
  } catch (err) {
    createSubjectError.value = (err as { message?: string }).message ?? 'Could not create subject.'
  } finally {
    creatingSubject.value = false
  }
}

async function handleDeleteSubject() {
  if (!deletingSubject.value) return
  deleting.value = true
  try {
    await globalAdminApi.deleteSubject(deletingSubject.value.id)
    subjects.value = await globalAdminApi.getSubjects()
    deletingSubject.value = null
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="global-admin-subjects">
    <div class="header">
      <h1 class="page-title">Subjects</h1>
      <p class="page-subtitle">Cross-school</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <template v-else>
      <div class="filters">
        <SelectField v-model="subjectSchoolFilter" label="School" :options="schoolFilterOptions" />
      </div>

      <div class="table-card">
        <table class="subjects-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>School</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="subject in filteredSubjects" :key="subject.id">
              <td class="subject-name">{{ subject.name }}</td>
              <td>{{ subject.schoolName }}</td>
              <td class="actions">
                <button class="link-btn danger" @click="deletingSubject = subject">Delete</button>
              </td>
            </tr>
            <tr v-if="filteredSubjects.length === 0">
              <td colspan="3" class="empty">{{ subjects.length === 0 ? 'No subjects yet.' : 'No subjects match this filter.' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-card code-card">
        <div class="code-card-header">
          <h2 class="section-title">Create a subject</h2>
        </div>
        <form class="create-row" @submit.prevent="handleCreateSubject">
          <SelectField
            v-model="newSubjectSchoolId"
            label="School"
            placeholder="Select school"
            :options="schoolOptions"
            searchable
          />
          <Field v-model="newSubjectName" label="Subject name" placeholder="Math" />
          <button
            type="submit"
            class="regen-btn create-btn"
            :disabled="creatingSubject || !newSubjectSchoolId || !newSubjectName.trim()"
          >
            {{ creatingSubject ? 'Creating…' : 'Add subject' }}
          </button>
        </form>
        <p v-if="createSubjectError" class="create-error">{{ createSubjectError }}</p>
      </div>
    </template>

    <ConfirmModal
      v-if="deletingSubject"
      title="Delete subject"
      :message="`Delete subject '${deletingSubject.name}'? Classes tagged with this subject may be affected.`"
      :loading="deleting"
      @close="deletingSubject = null"
      @confirm="handleDeleteSubject"
    />
  </div>
</template>

<style scoped>
.global-admin-subjects {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.header {
  display: flex;
  flex-direction: column;
  gap: 4px;
  position: sticky;
  top: -40px;
  z-index: 2;
  padding: 4px 0 12px;
  background: var(--paper);
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
}

.page-subtitle {
  margin: 0;
  font-size: 14px;
  color: var(--muted);
}

.loading,
.state-msg {
  color: var(--muted);
  font-size: 14px;
}

.state-msg.error {
  color: var(--t-lit);
}

.filters {
  display: flex;
  gap: 14px;
  flex-wrap: wrap;
}

.filters > * {
  min-width: 200px;
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
