<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as studentApi from '../api/student'
import type { ApiError, SavedExamSummary } from '../api/types'
import Field from '../components/shared/Field.vue'
import AppShell from '../components/shell/AppShell.vue'
import { useStudentNav } from '../composables/useStudentNav'

const route = useRoute()
const router = useRouter()
const { navItems } = useStudentNav()

const topic = ref(typeof route.query.topic === 'string' ? route.query.topic : '')
const generating = ref(false)
const generateError = ref<string | null>(null)

const exams = ref<SavedExamSummary[]>([])
const loadingExams = ref(true)

onMounted(async () => {
  try {
    exams.value = await studentApi.getExams()
  } finally {
    loadingExams.value = false
  }
})

async function handleGenerate() {
  if (!topic.value.trim() || generating.value) return
  generateError.value = null
  generating.value = true
  try {
    const res = await studentApi.generateExam(topic.value.trim())
    router.push(`/app/student/exams/${res.id}`)
  } catch (err) {
    const apiError = err as ApiError
    generateError.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not generate exam.'
  } finally {
    generating.value = false
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <div class="exams-view">
      <h1 class="page-title">Practice Exams</h1>
      <p class="page-subtitle">
        Type any topic you want to drill — even one you've mostly mastered but want more practice on.
      </p>

      <form class="generate-form" @submit.prevent="handleGenerate">
        <Field v-model="topic" label="Topic" placeholder="e.g. Photosynthesis, Quadratic equations…" />
        <button type="submit" class="generate-btn" :disabled="generating || !topic.trim()">
          {{ generating ? 'Generating…' : 'Generate Exam' }}
        </button>
      </form>
      <p v-if="generateError" class="generate-error">{{ generateError }}</p>

      <div class="history">
        <h2 class="history-title">History</h2>
        <div v-if="loadingExams" class="state-msg">Loading…</div>
        <p v-else-if="exams.length === 0" class="state-msg">No exams generated yet.</p>
        <ul v-else class="exam-list">
          <li v-for="exam in exams" :key="exam.id">
            <router-link :to="`/app/student/exams/${exam.id}`" class="exam-row">
              <span class="exam-topic">{{ exam.topic }}</span>
              <span class="exam-date">{{ formatDate(exam.createdAt) }}</span>
            </router-link>
          </li>
        </ul>
      </div>
    </div>
  </AppShell>
</template>

<style scoped>
.exams-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 640px;
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

.generate-form {
  display: flex;
  align-items: flex-end;
  gap: 12px;
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 20px;
}

.generate-form :deep(.field) {
  flex: 1;
}

.generate-btn {
  border: none;
  border-radius: var(--r-sm);
  background: var(--green-deep);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 14px;
  font-weight: 600;
  padding: 11px 18px;
  cursor: pointer;
  white-space: nowrap;
}

.generate-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.generate-error {
  margin: 0;
  font-size: 13px;
  color: var(--t-lit);
}

.history {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.history-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 16px;
  color: var(--ink);
}

.state-msg {
  font-size: 14px;
  color: var(--muted);
}

.exam-list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.exam-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-radius: var(--r-sm);
  background: var(--card);
  border: 1px solid var(--line);
  text-decoration: none;
  color: var(--ink);
}

.exam-row:hover {
  border-color: var(--green-br);
}

.exam-topic {
  font-weight: 600;
}

.exam-date {
  font-size: 13px;
  color: var(--muted);
}
</style>
