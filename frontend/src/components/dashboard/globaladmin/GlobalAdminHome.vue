<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import type { AdminClassSummary, AdminSubjectSummary, AdminUserSummary, ApiError, SchoolSummary } from '../../../api/types'
import { useAuthStore } from '../../../stores/auth'
import StatsCard from './StatsCard.vue'

const authStore = useAuthStore()

const schools = ref<SchoolSummary[]>([])
const users = ref<AdminUserSummary[]>([])
const classes = ref<AdminClassSummary[]>([])
const subjects = ref<AdminSubjectSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

function countByRole(role: string): number {
  return users.value.filter((u) => u.role === role).length
}

const peopleStats = computed(() => [
  { label: 'Total users', value: users.value.length },
  { label: 'Students', value: countByRole('Student') },
  { label: 'Teachers', value: countByRole('Teacher') },
  { label: 'School Admins', value: countByRole('SchoolAdmin') },
  { label: 'Global Admins', value: countByRole('Admin') },
])

const schoolsStats = computed(() => [
  { label: 'Schools', value: schools.value.length },
  { label: 'Classes', value: classes.value.length },
  { label: 'Subjects', value: subjects.value.length },
])

onMounted(async () => {
  loading.value = true
  error.value = null
  try {
    const [schoolsRes, usersRes, classesRes, subjectsRes] = await Promise.all([
      globalAdminApi.getSchools(),
      globalAdminApi.getUsers(),
      globalAdminApi.getClasses(),
      globalAdminApi.getSubjects(),
    ])
    schools.value = schoolsRes
    users.value = usersRes
    classes.value = classesRes
    subjects.value = subjectsRes
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load statistics.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="global-admin-home">
    <div class="welcome">
      <h1 class="page-title">Welcome back, {{ authStore.user?.fullName }}</h1>
      <p class="page-subtitle">Cross-school overview</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <div v-else class="card-grid">
      <StatsCard title="People" :stats="peopleStats" />
      <StatsCard title="Schools & Curriculum" :stats="schoolsStats" />
    </div>
  </div>
</template>

<style scoped>
.global-admin-home {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.welcome {
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

.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
}
</style>
