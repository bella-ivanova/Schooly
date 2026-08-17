<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminClassSummary, AdminSubjectSummary, AdminUserSummary, SchoolTeacherCode } from '../../../api/types'
import Field from '../../shared/Field.vue'
import SelectField from '../../shared/SelectField.vue'

const schoolName = ref<string | null>(null)
const classes = ref<AdminClassSummary[]>([])
const users = ref<AdminUserSummary[]>([])
const subjects = ref<AdminSubjectSummary[]>([])
const teacherCode = ref<SchoolTeacherCode | null>(null)
const loading = ref(true)
const regenerating = ref(false)

const newClassName = ref('')
const newClassSubjectId = ref('')
const newClassTeacherId = ref('')
const creatingClass = ref(false)
const createClassError = ref<string | null>(null)

const newSubjectName = ref('')
const creatingSubject = ref(false)
const createSubjectError = ref<string | null>(null)

const teacherOptions = computed(() =>
  users.value
    .filter((u) => u.role === 'Teacher')
    .map((t) => ({ value: t.id, label: t.fullName || t.username })),
)

const subjectOptions = computed(() =>
  subjects.value.map((s) => ({ value: String(s.id), label: s.name })),
)

onMounted(async () => {
  const [school, classesRes, usersRes, subjectsRes, codeRes] = await Promise.all([
    schoolAdminApi.getSchool(),
    schoolAdminApi.getClasses(),
    schoolAdminApi.getUsers(),
    schoolAdminApi.getSubjects(),
    schoolAdminApi.getTeacherCode(),
  ])
  schoolName.value = school.name
  classes.value = classesRes
  users.value = usersRes
  subjects.value = subjectsRes
  teacherCode.value = codeRes
  loading.value = false
})

async function handleRegenerateCode() {
  regenerating.value = true
  try {
    teacherCode.value = await schoolAdminApi.regenerateTeacherCode()
  } finally {
    regenerating.value = false
  }
}

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

async function handleCreateClass() {
  if (!newClassName.value.trim() || !newClassSubjectId.value) return
  createClassError.value = null
  creatingClass.value = true
  try {
    await schoolAdminApi.createClass(
      newClassName.value.trim(),
      Number(newClassSubjectId.value),
      newClassTeacherId.value || undefined,
    )
    classes.value = await schoolAdminApi.getClasses()
    newClassName.value = ''
    newClassSubjectId.value = ''
    newClassTeacherId.value = ''
  } catch (err) {
    createClassError.value = (err as { message?: string }).message ?? 'Could not create class.'
  } finally {
    creatingClass.value = false
  }
}
</script>

<template>
  <div class="school-admin-home">
    <div class="header">
      <h1 class="page-title">Classes</h1>
      <p class="page-subtitle">{{ schoolName }}</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else class="table-card">
      <table class="classes-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Subject</th>
            <th>Homeroom</th>
            <th>Students</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="cls in classes" :key="cls.id">
            <td class="class-name">{{ cls.name }}</td>
            <td>{{ cls.subjectName ?? '—' }}</td>
            <td>{{ cls.homeroomTeacherUsername ?? '—' }}</td>
            <td>{{ cls.studentCount }}</td>
            <td class="actions">Set homeroom · Add/remove student</td>
          </tr>
          <tr v-if="classes.length === 0">
            <td colspan="5" class="empty">No classes yet.</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="!loading" class="table-card code-card">
      <div class="code-card-header">
        <h2 class="section-title">Subjects</h2>
        <p class="section-subtitle">Add a subject before creating a class tagged to it.</p>
      </div>
      <form class="create-class-row" @submit.prevent="handleCreateSubject">
        <Field v-model="newSubjectName" label="Subject name" placeholder="Math" />
        <button type="submit" class="regen-btn create-btn" :disabled="creatingSubject || !newSubjectName.trim()">
          {{ creatingSubject ? 'Creating…' : 'Add subject' }}
        </button>
      </form>
      <p v-if="createSubjectError" class="create-class-error">{{ createSubjectError }}</p>
    </div>

    <div v-if="!loading" class="table-card code-card">
      <div class="code-card-header">
        <h2 class="section-title">Create a class</h2>
        <p class="section-subtitle">Create a class tagged with a subject — its teacher can generate a code from it for students to join.</p>
      </div>
      <form class="create-class-row" @submit.prevent="handleCreateClass">
        <Field v-model="newClassName" label="Class name" placeholder="10A" />
        <SelectField
          v-model="newClassSubjectId"
          label="Subject"
          placeholder="Select subject"
          :options="subjectOptions"
        />
        <SelectField
          v-model="newClassTeacherId"
          label="Homeroom teacher"
          placeholder="Select teacher"
          :options="teacherOptions"
        />
        <button
          type="submit"
          class="regen-btn create-btn"
          :disabled="creatingClass || !newClassName.trim() || !newClassSubjectId"
        >
          {{ creatingClass ? 'Creating…' : 'Create class' }}
        </button>
      </form>
      <p v-if="createClassError" class="create-class-error">{{ createClassError }}</p>
    </div>

    <div v-if="!loading" class="table-card code-card">
      <div class="code-card-header">
        <h2 class="section-title">Teacher registration code</h2>
        <p class="section-subtitle">Share this code with teachers joining your school. Regenerating invalidates the old code immediately.</p>
      </div>
      <div class="code-row">
        <code v-if="teacherCode" class="code-value">{{ teacherCode.code }}</code>
        <span v-else class="code-empty">No code generated yet.</span>
        <button class="regen-btn" :disabled="regenerating" @click="handleRegenerateCode">
          {{ regenerating ? 'Generating…' : (teacherCode ? 'Regenerate' : 'Generate') }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.school-admin-home {
  display: flex;
  flex-direction: column;
  gap: 20px;
  max-width: 960px;
}

.header {
  display: flex;
  flex-direction: column;
  gap: 4px;
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
  color: var(--green-deep);
  font-size: 13px;
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

.code-row {
  display: flex;
  align-items: center;
  gap: 14px;
  flex-wrap: wrap;
}

.create-class-row {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  flex-wrap: wrap;
}

.create-class-row > * {
  min-width: 180px;
}

.create-btn {
  height: 42px;
}

.create-class-error {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}

.code-value {
  font-family: monospace;
  font-size: 15px;
  letter-spacing: 0.05em;
  background: var(--cream-2);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 8px 14px;
  color: var(--ink);
}

.code-empty {
  font-size: 14px;
  color: var(--muted);
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
