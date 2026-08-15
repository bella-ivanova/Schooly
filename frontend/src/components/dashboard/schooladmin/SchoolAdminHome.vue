<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminClassSummary } from '../../../api/types'

const schoolName = ref<string | null>(null)
const classes = ref<AdminClassSummary[]>([])
const loading = ref(true)

onMounted(async () => {
  const [school, classesRes] = await Promise.all([schoolAdminApi.getSchool(), schoolAdminApi.getClasses()])
  schoolName.value = school.name
  classes.value = classesRes
  loading.value = false
})
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
</style>
