<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import type { AdminUserSummary, ApiError, SchoolSummary } from '../../../api/types'
import SelectField from '../../shared/SelectField.vue'
import MakeSchoolAdminModal from './MakeSchoolAdminModal.vue'

const users = ref<AdminUserSummary[]>([])
const schools = ref<SchoolSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const roleFilter = ref('')
const schoolFilter = ref('')
const gradeFilter = ref('')

watch(roleFilter, () => {
  gradeFilter.value = ''
})

const promotingUser = ref<AdminUserSummary | null>(null)

const roleOptions = [
  { value: '', label: 'All roles' },
  { value: 'Student', label: 'Student' },
  { value: 'Teacher', label: 'Teacher' },
  { value: 'SchoolAdmin', label: 'School Admin' },
  { value: 'Admin', label: 'Admin' },
]

const schoolOptions = computed(() => [
  { value: '', label: 'All schools' },
  ...schools.value.map((s) => ({ value: String(s.id), label: s.name })),
])

const makeAdminSchoolOptions = computed(() => schools.value.map((s) => ({ value: String(s.id), label: s.name })))

const gradeOptions = computed(() => {
  const grades = Array.from(
    new Set(users.value.filter((u) => u.role === 'Student' && u.grade != null).map((u) => u.grade as number)),
  ).sort((a, b) => a - b)
  return [{ value: '', label: 'All grades' }, ...grades.map((g) => ({ value: String(g), label: `Grade ${g}` }))]
})

const filteredUsers = computed(() =>
  users.value.filter((u) => {
    if (roleFilter.value && u.role !== roleFilter.value) return false
    if (schoolFilter.value && String(u.schoolId ?? '') !== schoolFilter.value) return false
    if (roleFilter.value === 'Student' && gradeFilter.value && String(u.grade ?? '') !== gradeFilter.value) return false
    return true
  }),
)

async function loadUsers() {
  users.value = await globalAdminApi.getUsers()
}

onMounted(async () => {
  loading.value = true
  error.value = null
  try {
    const [usersRes, schoolsRes] = await Promise.all([globalAdminApi.getUsers(), globalAdminApi.getSchools()])
    users.value = usersRes
    schools.value = schoolsRes
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load users.'
  } finally {
    loading.value = false
  }
})

async function handlePromoted() {
  await loadUsers()
}
</script>

<template>
  <div class="global-admin-users">
    <div class="header">
      <h1 class="page-title">All Users</h1>
      <p class="page-subtitle">Cross-school</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <template v-else>
      <div class="filters">
        <SelectField v-model="roleFilter" label="Role" :options="roleOptions" />
        <SelectField v-if="roleFilter === 'Student'" v-model="gradeFilter" label="Grade" :options="gradeOptions" />
        <SelectField v-model="schoolFilter" label="School" :options="schoolOptions" searchable />
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
              <th>School</th>
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
              <td>{{ user.schoolName ?? '—' }}</td>
              <td class="actions">
                <button
                  v-if="user.role !== 'Admin' && user.role !== 'SchoolAdmin'"
                  class="link-btn"
                  @click="promotingUser = user"
                >
                  Make School Admin
                </button>
              </td>
            </tr>
            <tr v-if="filteredUsers.length === 0">
              <td colspan="7" class="empty">No users match this filter.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <MakeSchoolAdminModal
      v-if="promotingUser"
      :user-id="promotingUser.id"
      :user-label="promotingUser.fullName || promotingUser.username"
      :school-options="makeAdminSchoolOptions"
      @close="promotingUser = null"
      @promoted="handlePromoted"
    />
  </div>
</template>

<style scoped>
.global-admin-users {
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
