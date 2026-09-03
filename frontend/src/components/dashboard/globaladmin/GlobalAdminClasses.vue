<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import type { AdminClassSummary, AdminSubjectSummary, AdminUserSummary, ApiError, SchoolSummary } from '../../../api/types'
import Field from '../../shared/Field.vue'
import SelectField from '../../shared/SelectField.vue'
import ConfirmModal from '../../shared/ConfirmModal.vue'
import AssignStudentModal from './AssignStudentModal.vue'
import AssignTeacherModal from './AssignTeacherModal.vue'

const classes = ref<AdminClassSummary[]>([])
const schools = ref<SchoolSummary[]>([])
const users = ref<AdminUserSummary[]>([])
const subjects = ref<AdminSubjectSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const newClassSchoolId = ref('')
const newClassName = ref('')
const newClassSubjectId = ref('')
const newClassTeacherId = ref('')
const newClassGrade = ref('')
const creatingClass = ref(false)
const createClassError = ref<string | null>(null)

const gradeOptions = Array.from({ length: 12 }, (_, i) => ({
  value: String(i + 1),
  label: `Grade ${i + 1}`,
}))

const deletingClass = ref<AdminClassSummary | null>(null)
const deleting = ref(false)

const assigningStudentClass = ref<AdminClassSummary | null>(null)
const assigningTeacherClass = ref<AdminClassSummary | null>(null)

const classSchoolFilter = ref('')
const classSubjectFilter = ref('')
const classGradeFilter = ref('')

const schoolOptions = computed(() => schools.value.map((s) => ({ value: String(s.id), label: s.name })))

const schoolFilterOptions = computed(() => [{ value: '', label: 'All schools' }, ...schoolOptions.value])

const subjectFilterOptions = computed(() => {
  const names = Array.from(new Set(classes.value.map((c) => c.subjectName).filter((n): n is string => !!n))).sort()
  return [{ value: '', label: 'All subjects' }, ...names.map((n) => ({ value: n, label: n }))]
})

const gradeFilterOptions = computed(() => {
  const grades = Array.from(new Set(classes.value.map((c) => c.grade).filter((g): g is number => g != null))).sort(
    (a, b) => a - b,
  )
  return [{ value: '', label: 'All grades' }, ...grades.map((g) => ({ value: String(g), label: `Grade ${g}` }))]
})

const filteredClasses = computed(() =>
  classes.value.filter((c) => {
    if (classSchoolFilter.value && String(c.schoolId) !== classSchoolFilter.value) return false
    if (classSubjectFilter.value && c.subjectName !== classSubjectFilter.value) return false
    if (classGradeFilter.value && String(c.grade) !== classGradeFilter.value) return false
    return true
  }),
)

const subjectOptionsForNewClass = computed(() =>
  newClassSchoolId.value
    ? subjects.value
        .filter((s) => String(s.schoolId) === newClassSchoolId.value)
        .map((s) => ({ value: String(s.id), label: s.name }))
    : [],
)

const teacherOptionsForNewClass = computed(() =>
  newClassSchoolId.value
    ? users.value
        .filter((u) => u.role === 'Teacher' && String(u.schoolId) === newClassSchoolId.value)
        .map((t) => ({ value: t.id, label: t.fullName || t.username }))
    : [],
)

function studentOptionsForSchool(schoolId: number) {
  return users.value
    .filter((u) => u.role === 'Student' && u.schoolId === schoolId)
    .map((s) => ({ value: s.id, label: s.fullName || s.username }))
}

function teacherOptionsForSchool(schoolId: number) {
  return users.value
    .filter((u) => u.role === 'Teacher' && u.schoolId === schoolId)
    .map((t) => ({ value: t.id, label: t.fullName || t.username }))
}

async function loadAll() {
  const [classesRes, schoolsRes, usersRes, subjectsRes] = await Promise.all([
    globalAdminApi.getClasses(),
    globalAdminApi.getSchools(),
    globalAdminApi.getUsers(),
    globalAdminApi.getSubjects(),
  ])
  classes.value = classesRes
  schools.value = schoolsRes
  users.value = usersRes
  subjects.value = subjectsRes
}

onMounted(async () => {
  loading.value = true
  error.value = null
  try {
    await loadAll()
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load classes.'
  } finally {
    loading.value = false
  }
})

async function handleCreateClass() {
  if (!newClassSchoolId.value || !newClassName.value.trim() || !newClassSubjectId.value) return
  createClassError.value = null
  creatingClass.value = true
  try {
    await globalAdminApi.createClass(
      Number(newClassSchoolId.value),
      newClassName.value.trim(),
      Number(newClassSubjectId.value),
      newClassTeacherId.value || undefined,
      newClassGrade.value ? Number(newClassGrade.value) : undefined,
    )
    classes.value = await globalAdminApi.getClasses()
    newClassName.value = ''
    newClassSubjectId.value = ''
    newClassTeacherId.value = ''
    newClassGrade.value = ''
  } catch (err) {
    createClassError.value = (err as { message?: string }).message ?? 'Could not create class.'
  } finally {
    creatingClass.value = false
  }
}

async function handleDeleteClass() {
  if (!deletingClass.value) return
  deleting.value = true
  try {
    await globalAdminApi.deleteClass(deletingClass.value.id)
    classes.value = await globalAdminApi.getClasses()
    deletingClass.value = null
  } finally {
    deleting.value = false
  }
}

async function handleAssigned() {
  classes.value = await globalAdminApi.getClasses()
}
</script>

<template>
  <div class="global-admin-classes">
    <div class="header">
      <h1 class="page-title">All Classes</h1>
      <p class="page-subtitle">Cross-school</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <template v-else>
      <div class="filters">
        <SelectField v-model="classSchoolFilter" label="School" :options="schoolFilterOptions" />
        <SelectField v-model="classSubjectFilter" label="Subject" :options="subjectFilterOptions" />
        <SelectField v-model="classGradeFilter" label="Grade" :options="gradeFilterOptions" />
      </div>

      <div class="table-card">
        <table class="classes-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>School</th>
              <th>Grade</th>
              <th>Subject</th>
              <th>Homeroom</th>
              <th>Students</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="cls in filteredClasses" :key="cls.id">
              <td class="class-name">{{ cls.name }}</td>
              <td>{{ cls.schoolName }}</td>
              <td>{{ cls.grade ?? '—' }}</td>
              <td>{{ cls.subjectName ?? '—' }}</td>
              <td>{{ cls.homeroomTeacherUsername ?? '—' }}</td>
              <td>{{ cls.studentCount }}</td>
              <td class="actions">
                <button class="link-btn" @click="assigningStudentClass = cls">Assign student</button>
                <button class="link-btn" @click="assigningTeacherClass = cls">Assign teacher</button>
                <button class="link-btn danger" @click="deletingClass = cls">Delete</button>
              </td>
            </tr>
            <tr v-if="filteredClasses.length === 0">
              <td colspan="7" class="empty">No classes match this filter.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-card code-card">
        <div class="code-card-header">
          <h2 class="section-title">Create a class</h2>
          <p class="section-subtitle">The subject must already exist for the chosen school.</p>
        </div>
        <form class="create-row" @submit.prevent="handleCreateClass">
          <SelectField
            v-model="newClassSchoolId"
            label="School"
            placeholder="Select school"
            :options="schoolOptions"
            searchable
          />
          <Field v-model="newClassName" label="Class name" placeholder="10A" />
          <SelectField
            v-model="newClassGrade"
            label="Grade"
            placeholder="Select grade"
            :options="gradeOptions"
          />
          <SelectField
            v-model="newClassSubjectId"
            label="Subject"
            placeholder="Select subject"
            :options="subjectOptionsForNewClass"
          />
          <SelectField
            v-model="newClassTeacherId"
            label="Homeroom teacher"
            placeholder="Select teacher"
            :options="teacherOptionsForNewClass"
            searchable
          />
          <button
            type="submit"
            class="regen-btn create-btn"
            :disabled="creatingClass || !newClassSchoolId || !newClassName.trim() || !newClassSubjectId"
          >
            {{ creatingClass ? 'Creating…' : 'Create class' }}
          </button>
        </form>
        <p v-if="newClassSchoolId && subjectOptionsForNewClass.length === 0" class="hint">
          No subjects yet for this school — create one on the Subjects page first.
        </p>
        <p v-if="createClassError" class="create-error">{{ createClassError }}</p>
      </div>
    </template>

    <ConfirmModal
      v-if="deletingClass"
      title="Delete class"
      :message="`Delete class '${deletingClass.name}'? This cannot be undone.`"
      :loading="deleting"
      @close="deletingClass = null"
      @confirm="handleDeleteClass"
    />

    <AssignStudentModal
      v-if="assigningStudentClass"
      :class-id="assigningStudentClass.id"
      :student-options="studentOptionsForSchool(assigningStudentClass.schoolId)"
      @close="assigningStudentClass = null"
      @assigned="handleAssigned"
    />

    <AssignTeacherModal
      v-if="assigningTeacherClass"
      :class-id="assigningTeacherClass.id"
      :school-id="assigningTeacherClass.schoolId"
      :teacher-options="teacherOptionsForSchool(assigningTeacherClass.schoolId)"
      @close="assigningTeacherClass = null"
      @assigned="handleAssigned"
    />
  </div>
</template>

<style scoped>
.global-admin-classes {
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

.classes-table {
  width: 100%;
  border-collapse: collapse;
}

.classes-table th {
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--muted);
  background: var(--cream-2);
  padding: 14px 20px;
}

.classes-table td {
  padding: 14px 20px;
  font-size: 14px;
  color: var(--ink-2);
  border-top: 1px solid var(--line);
}

.class-name {
  font-weight: 600;
  color: var(--ink);
}

.actions {
  display: flex;
  gap: 14px;
  align-items: center;
}

.link-btn {
  background: none;
  border: none;
  color: var(--green-deep);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
  white-space: nowrap;
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
  min-width: 180px;
}

.create-btn {
  height: 42px;
}

.hint,
.create-error {
  margin: 0;
  font-size: 13px;
  color: var(--muted);
}

.create-error {
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
