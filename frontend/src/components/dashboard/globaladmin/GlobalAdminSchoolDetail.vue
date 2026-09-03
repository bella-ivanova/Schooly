<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import * as globalAdminApi from '../../../api/globalAdmin'
import type { AdminClassSummary, AdminSubjectSummary, AdminUserSummary, ApiError, SchoolSummary } from '../../../api/types'
import SelectField from '../../shared/SelectField.vue'

const route = useRoute()
const schoolId = computed(() => Number(route.params.schoolId))

const school = ref<SchoolSummary | null>(null)
const classes = ref<AdminClassSummary[]>([])
const subjects = ref<AdminSubjectSummary[]>([])
const users = ref<AdminUserSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const schoolClasses = computed(() => classes.value.filter((c) => c.schoolId === schoolId.value))
const schoolSubjects = computed(() => subjects.value.filter((s) => s.schoolId === schoolId.value))
const schoolRoster = computed(() =>
  users.value.filter((u) => u.schoolId === schoolId.value && (u.role === 'Teacher' || u.role === 'SchoolAdmin')),
)
const schoolStudents = computed(() => users.value.filter((u) => u.schoolId === schoolId.value && u.role === 'Student'))

const gradeFilter = ref('')
const subjectFilter = ref('')

watch(schoolId, () => {
  gradeFilter.value = ''
  subjectFilter.value = ''
})

const gradeOptions = computed(() => {
  const grades = Array.from(
    new Set(schoolStudents.value.filter((u) => u.grade != null).map((u) => u.grade as number)),
  ).sort((a, b) => a - b)
  return [{ value: '', label: 'All grades' }, ...grades.map((g) => ({ value: String(g), label: `Grade ${g}` }))]
})

const rosterSubjectOptions = computed(() => {
  const names = Array.from(new Set(schoolRoster.value.flatMap((u) => u.subjects))).sort()
  return [{ value: '', label: 'All subjects' }, ...names.map((n) => ({ value: n, label: n }))]
})

const filteredSchoolStudents = computed(() =>
  gradeFilter.value ? schoolStudents.value.filter((u) => String(u.grade ?? '') === gradeFilter.value) : schoolStudents.value,
)

const filteredSchoolRoster = computed(() =>
  subjectFilter.value ? schoolRoster.value.filter((u) => u.subjects.includes(subjectFilter.value)) : schoolRoster.value,
)

const createdLabel = computed(() =>
  school.value ? new Date(school.value.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '',
)

async function load() {
  loading.value = true
  error.value = null
  try {
    const [schoolsRes, classesRes, subjectsRes, usersRes] = await Promise.all([
      globalAdminApi.getSchools(),
      globalAdminApi.getClasses(),
      globalAdminApi.getSubjects(),
      globalAdminApi.getUsers(),
    ])
    school.value = schoolsRes.find((s) => s.id === schoolId.value) ?? null
    classes.value = classesRes
    subjects.value = subjectsRes
    users.value = usersRes
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load this school.'
  } finally {
    loading.value = false
  }
}

watch(schoolId, load, { immediate: true })
</script>

<template>
  <div class="school-detail">
    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <div v-else-if="!school" class="state-msg error">School not found.</div>
    <template v-else>
      <div class="header">
        <h1 class="page-title">{{ school.name }}</h1>
        <p class="subtitle">
          Created {{ createdLabel }} · {{ school.studentCount }} students · {{ school.teacherCount }} teachers
        </p>
      </div>

      <div class="table-card">
        <div class="section-header"><h2 class="section-title">Classes</h2></div>
        <table class="detail-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Subject</th>
              <th>Homeroom</th>
              <th>Students</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="cls in schoolClasses" :key="cls.id">
              <td class="row-name">{{ cls.name }}</td>
              <td>{{ cls.subjectName ?? '—' }}</td>
              <td>{{ cls.homeroomTeacherUsername ?? '—' }}</td>
              <td>{{ cls.studentCount }}</td>
            </tr>
            <tr v-if="schoolClasses.length === 0">
              <td colspan="4" class="empty">No classes yet.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-card">
        <div class="section-header"><h2 class="section-title">Subjects</h2></div>
        <table class="detail-table">
          <thead>
            <tr>
              <th>Name</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="subject in schoolSubjects" :key="subject.id">
              <td class="row-name">{{ subject.name }}</td>
            </tr>
            <tr v-if="schoolSubjects.length === 0">
              <td colspan="1" class="empty">No subjects yet.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-card">
        <div class="section-header filterable">
          <h2 class="section-title">Students</h2>
          <SelectField v-model="gradeFilter" label="Grade" :options="gradeOptions" />
        </div>
        <table class="detail-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Full name</th>
              <th>Grade</th>
              <th>Classes</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in filteredSchoolStudents" :key="user.id">
              <td class="row-name">{{ user.username }}</td>
              <td>{{ user.fullName }}</td>
              <td>{{ user.grade ?? '—' }}</td>
              <td>{{ user.classNames.length ? user.classNames.join(', ') : '—' }}</td>
            </tr>
            <tr v-if="filteredSchoolStudents.length === 0">
              <td colspan="4" class="empty">{{ schoolStudents.length === 0 ? 'No students yet.' : 'No students match this filter.' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-card">
        <div class="section-header filterable">
          <h2 class="section-title">Teacher & Admin roster</h2>
          <SelectField v-model="subjectFilter" label="Subject" :options="rosterSubjectOptions" />
        </div>
        <table class="detail-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Full name</th>
              <th>Role</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in filteredSchoolRoster" :key="user.id">
              <td class="row-name">{{ user.username }}</td>
              <td>{{ user.fullName }}</td>
              <td>{{ user.role }}</td>
            </tr>
            <tr v-if="filteredSchoolRoster.length === 0">
              <td colspan="3" class="empty">{{ schoolRoster.length === 0 ? 'No teachers or admins yet.' : 'No one matches this filter.' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>

<style scoped>
.school-detail {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.loading,
.state-msg {
  font-size: 14px;
  color: var(--muted);
}

.state-msg.error {
  color: var(--t-lit);
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

.subtitle {
  margin: 0;
  font-size: 14px;
  color: var(--muted);
}

.table-card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  overflow: hidden;
}

.section-header {
  padding: 14px 20px;
}

.section-header.filterable {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  flex-wrap: wrap;
}

.section-header.filterable :deep(.field) {
  min-width: 180px;
}

.section-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 16px;
  color: var(--ink);
}

.detail-table {
  width: 100%;
  border-collapse: collapse;
}

.detail-table th {
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--muted);
  background: var(--cream-2);
  padding: 12px 20px;
}

.detail-table td {
  padding: 12px 20px;
  font-size: 14px;
  color: var(--ink-2);
  border-top: 1px solid var(--line);
}

.row-name {
  font-weight: 600;
  color: var(--ink);
}

.empty {
  text-align: center;
  color: var(--muted);
}
</style>
