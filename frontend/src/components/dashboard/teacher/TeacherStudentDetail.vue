<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import * as teacherApi from '../../../api/teacher'
import type { ApiError, TeacherStudentStats, TeacherStudentSummary } from '../../../api/types'
import StudentStatsCard from './StudentStatsCard.vue'

const route = useRoute()
const studentId = computed(() => String(route.params.studentId))

const student = ref<TeacherStudentSummary | null>(null)
const stats = ref<TeacherStudentStats | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    const [studentRes, statsRes] = await Promise.all([
      teacherApi.getStudentDetail(studentId.value),
      teacherApi.getStudentStats(studentId.value),
    ])
    student.value = studentRes
    stats.value = statsRes
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load this student.'
  } finally {
    loading.value = false
  }
}

watch(studentId, load, { immediate: true })
</script>

<template>
  <div class="student-detail">
    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <template v-else-if="student">
      <div class="header">
        <h1 class="page-title">{{ student.fullName }}</h1>
        <p v-if="student.grade" class="subtitle">Grade {{ student.grade }}</p>
      </div>

      <div class="classes-card">
        <h2 class="section-title">Classes with you</h2>
        <ul class="class-list">
          <li v-for="name in student.classNames" :key="name">{{ name }}</li>
        </ul>
      </div>

      <StudentStatsCard v-if="stats" :stats="stats" />
    </template>
  </div>
</template>

<style scoped>
.student-detail {
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-width: 1400px;
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

.classes-card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.section-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 18px;
  color: var(--ink);
}

.class-list {
  margin: 0;
  padding-left: 20px;
  font-size: 14px;
  color: var(--ink-2);
  display: flex;
  flex-direction: column;
  gap: 6px;
}
</style>
