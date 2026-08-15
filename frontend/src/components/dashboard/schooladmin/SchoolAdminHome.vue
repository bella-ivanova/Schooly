<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminClassSummary, SchoolTeacherCode } from '../../../api/types'

const schoolName = ref<string | null>(null)
const classes = ref<AdminClassSummary[]>([])
const teacherCode = ref<SchoolTeacherCode | null>(null)
const loading = ref(true)
const regenerating = ref(false)

onMounted(async () => {
  const [school, classesRes, codeRes] = await Promise.all([
    schoolAdminApi.getSchool(),
    schoolAdminApi.getClasses(),
    schoolAdminApi.getTeacherCode(),
  ])
  schoolName.value = school.name
  classes.value = classesRes
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
            <th>Homeroom</th>
            <th>Students</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="cls in classes" :key="cls.id">
            <td class="class-name">{{ cls.name }}</td>
            <td>{{ cls.homeroomTeacherUsername ?? '—' }}</td>
            <td>{{ cls.studentCount }}</td>
            <td class="actions">Set homeroom · Add/remove student</td>
          </tr>
          <tr v-if="classes.length === 0">
            <td colspan="4" class="empty">No classes yet.</td>
          </tr>
        </tbody>
      </table>
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
