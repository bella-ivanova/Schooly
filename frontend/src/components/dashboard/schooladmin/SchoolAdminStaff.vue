<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminSubjectSummary, AdminUserSummary } from '../../../api/types'
import Field from '../../shared/Field.vue'
import SelectField from '../../shared/SelectField.vue'
import TeacherSubjectsModal from './TeacherSubjectsModal.vue'

const users = ref<AdminUserSummary[]>([])
const subjects = ref<AdminSubjectSummary[]>([])
const loading = ref(true)

const roleFilter = ref('')
const gradeFilter = ref('')
const search = ref('')

watch(roleFilter, () => {
  gradeFilter.value = ''
})

const managingTeacher = ref<AdminUserSummary | null>(null)

const roleOptions = [
  { value: '', label: 'All roles' },
  { value: 'Student', label: 'Student' },
  { value: 'Teacher', label: 'Teacher' },
  { value: 'SchoolAdmin', label: 'School Admin' },
]

const subjectOptions = computed(() => subjects.value.map((s) => ({ value: String(s.id), label: s.name })))

const gradeOptions = computed(() => {
  const grades = Array.from(
    new Set(users.value.filter((u) => u.role === 'Student' && u.grade != null).map((u) => u.grade as number)),
  ).sort((a, b) => a - b)
  return [{ value: '', label: 'All grades' }, ...grades.map((g) => ({ value: String(g), label: `Grade ${g}` }))]
})

const filteredUsers = computed(() =>
  users.value.filter((u) => {
    if (roleFilter.value && u.role !== roleFilter.value) return false
    if (roleFilter.value === 'Student' && gradeFilter.value && String(u.grade ?? '') !== gradeFilter.value) return false
    const query = search.value.trim().toLowerCase()
    if (query && !u.username.toLowerCase().includes(query) && !u.fullName.toLowerCase().includes(query)) return false
    return true
  }),
)

async function loadUsers() {
  users.value = await schoolAdminApi.getUsers()
}

onMounted(async () => {
  const [usersRes, subjectsRes] = await Promise.all([schoolAdminApi.getUsers(), schoolAdminApi.getSubjects()])
  users.value = usersRes
  subjects.value = subjectsRes
  loading.value = false
})

async function handleSubjectsChanged() {
  await loadUsers()
}
</script>

<template>
  <div class="school-admin-staff">
    <h1 class="page-title">Staff &amp; Students</h1>

    <div v-if="loading" class="loading">Loading...</div>
    <template v-else>
      <div class="filters">
        <SelectField v-model="roleFilter" label="Role" :options="roleOptions" />
        <SelectField v-if="roleFilter === 'Student'" v-model="gradeFilter" label="Grade" :options="gradeOptions" />
        <Field v-model="search" label="Search" placeholder="Name or username" />
      </div>

      <div class="table-card">
        <table class="users-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Full name</th>
              <th>Role</th>
              <th>Grade</th>
              <th>Classes</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in filteredUsers" :key="user.id">
              <td class="user-name">{{ user.username }}</td>
              <td>{{ user.fullName }}</td>
              <td>{{ user.role }}</td>
              <td>{{ user.grade ?? '—' }}</td>
              <td>{{ user.classNames.length ? user.classNames.join(', ') : '—' }}</td>
              <td class="actions">
                <button v-if="user.role === 'Teacher'" class="link-btn" @click="managingTeacher = user">
                  Manage subjects
                </button>
              </td>
            </tr>
            <tr v-if="filteredUsers.length === 0">
              <td colspan="6" class="empty">No users match this filter.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <TeacherSubjectsModal
      v-if="managingTeacher"
      :teacher-id="managingTeacher.id"
      :teacher-label="managingTeacher.fullName || managingTeacher.username"
      :subject-options="subjectOptions"
      @close="managingTeacher = null"
      @changed="handleSubjectsChanged"
    />
  </div>
</template>

<style scoped>
.school-admin-staff {
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

.users-table {
  width: 100%;
  border-collapse: collapse;
}

.users-table th {
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--muted);
  background: var(--cream-2);
  padding: 14px 20px;
}

.users-table td {
  padding: 14px 20px;
  font-size: 14px;
  color: var(--ink-2);
  border-top: 1px solid var(--line);
}

.user-name {
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
  white-space: nowrap;
}

.empty {
  text-align: center;
  color: var(--muted);
}
</style>
