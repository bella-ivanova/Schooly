<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as globalAdminApi from '../../../api/globalAdmin'
import type { ApiError, SchoolSummary } from '../../../api/types'
import Field from '../../shared/Field.vue'
import SchoolCard from './SchoolCard.vue'

const router = useRouter()

const schools = ref<SchoolSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const newSchoolName = ref('')
const creatingSchool = ref(false)
const createSchoolError = ref<string | null>(null)

onMounted(async () => {
  loading.value = true
  error.value = null
  try {
    schools.value = await globalAdminApi.getSchools()
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load schools.'
  } finally {
    loading.value = false
  }
})

function openSchool(schoolId: number) {
  router.push(`/app/global-admin/schools/${schoolId}`)
}

async function handleCreateSchool() {
  if (!newSchoolName.value.trim()) return
  createSchoolError.value = null
  creatingSchool.value = true
  try {
    await globalAdminApi.createSchool(newSchoolName.value.trim())
    schools.value = await globalAdminApi.getSchools()
    newSchoolName.value = ''
  } catch (err) {
    createSchoolError.value = (err as { message?: string }).message ?? 'Could not create school.'
  } finally {
    creatingSchool.value = false
  }
}
</script>

<template>
  <div class="global-admin-schools">
    <div class="header">
      <h1 class="page-title">Schools</h1>
      <p class="page-subtitle">Unscoped · all schools</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <template v-else>
      <div v-if="schools.length === 0" class="empty">No schools registered yet.</div>
      <div v-else class="school-grid">
        <SchoolCard v-for="school in schools" :key="school.id" :school="school" @select="openSchool(school.id)" />
      </div>

      <div class="table-card code-card">
        <div class="code-card-header">
          <h2 class="section-title">Create a school</h2>
          <p class="section-subtitle">Registers a new school with no classes, subjects, or users yet.</p>
        </div>
        <form class="create-row" @submit.prevent="handleCreateSchool">
          <Field v-model="newSchoolName" label="School name" placeholder="Lincoln High School" />
          <button type="submit" class="regen-btn create-btn" :disabled="creatingSchool || !newSchoolName.trim()">
            {{ creatingSchool ? 'Creating…' : 'Create school' }}
          </button>
        </form>
        <p v-if="createSchoolError" class="create-error">{{ createSchoolError }}</p>
      </div>
    </template>
  </div>
</template>

<style scoped>
.global-admin-schools {
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
.empty,
.state-msg {
  color: var(--muted);
  font-size: 14px;
}

.state-msg.error {
  color: var(--t-lit);
}

.school-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
}

.table-card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
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
  min-width: 220px;
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
