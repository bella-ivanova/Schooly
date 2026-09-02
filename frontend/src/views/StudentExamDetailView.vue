<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import * as studentApi from '../api/student'
import type { ApiError, SavedExamDetail } from '../api/types'
import AppShell from '../components/shell/AppShell.vue'
import { useStudentNav } from '../composables/useStudentNav'
import { renderMarkdown } from '../utils/markdown'

const route = useRoute()
const { navItems } = useStudentNav()

const exam = ref<SavedExamDetail | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

async function load(id: number) {
  loading.value = true
  error.value = null
  try {
    exam.value = await studentApi.getExamDetail(id)
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load this exam.'
  } finally {
    loading.value = false
  }
}

watch(
  () => route.params.id,
  (id) => {
    const value = Array.isArray(id) ? id[0] : id
    if (value) load(Number(value))
  },
  { immediate: true },
)

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <div class="exam-detail">
      <router-link to="/app/student/exams" class="back-link">← Back to Practice Exams</router-link>

      <div v-if="loading" class="state-msg">Loading…</div>
      <div v-else-if="error" class="state-msg error">{{ error }}</div>
      <template v-else-if="exam">
        <div class="header">
          <h1 class="page-title">{{ exam.topic }}</h1>
          <p class="page-subtitle">Generated {{ formatDate(exam.createdAt) }}</p>
        </div>
        <div class="exam-content" v-html="renderMarkdown(exam.content)" />
      </template>
    </div>
  </AppShell>
</template>

<style scoped>
.exam-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 1400px;
}

.back-link {
  align-self: flex-start;
  font-size: 13px;
  font-weight: 600;
  color: var(--green-deep);
  text-decoration: none;
}

.back-link:hover {
  text-decoration: underline;
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
  font-size: 26px;
  color: var(--ink);
}

.page-subtitle {
  margin: 0;
  font-size: 13px;
  color: var(--muted);
}

.state-msg {
  font-size: 14px;
  color: var(--muted);
}

.state-msg.error {
  color: var(--t-lit);
}

.exam-content {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 24px 28px;
  font-size: 15px;
  line-height: 1.6;
  color: var(--ink);
}

.exam-content :deep(p) {
  margin: 0 0 12px;
}

.exam-content :deep(h1),
.exam-content :deep(h2),
.exam-content :deep(h3) {
  font-family: var(--font-heading);
  margin: 20px 0 10px;
}

.exam-content :deep(ul),
.exam-content :deep(ol) {
  margin: 0 0 12px;
  padding-left: 22px;
}
</style>
