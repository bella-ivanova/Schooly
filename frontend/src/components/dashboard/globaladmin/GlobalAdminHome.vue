<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import type { SchoolSummary } from '../../../api/types'
import SchoolCard from './SchoolCard.vue'

const schools = ref<SchoolSummary[]>([])
const loading = ref(true)

onMounted(async () => {
  schools.value = await globalAdminApi.getSchools()
  loading.value = false
})
</script>

<template>
  <div class="global-admin-home">
    <div class="header">
      <h1 class="page-title">Schools</h1>
      <p class="page-subtitle">Unscoped · all schools</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="schools.length === 0" class="empty">No schools registered yet.</div>
    <div v-else class="school-grid">
      <SchoolCard v-for="school in schools" :key="school.id" :school="school" />
    </div>
  </div>
</template>

<style scoped>
.global-admin-home {
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
.empty {
  color: var(--muted);
  font-size: 14px;
}

.school-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
}
</style>
