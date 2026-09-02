<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as teacherApi from '../../../api/teacher'
import type { ApiError, ClassJoinCode, TeacherClassSummary, TeacherRosterStudent, TeacherStruggleGroup } from '../../../api/types'
import ClassRosterCard from './ClassRosterCard.vue'
import StruggleTopicsCard from './StruggleTopicsCard.vue'
import MostActiveStudentsCard from './MostActiveStudentsCard.vue'

const route = useRoute()
const router = useRouter()
const classId = computed(() => Number(route.params.classId))

const classInfo = ref<TeacherClassSummary | null>(null)
const roster = ref<TeacherRosterStudent[]>([])
const struggles = ref<TeacherStruggleGroup[]>([])
const joinCode = ref<ClassJoinCode | null>(null)
const activity = ref<{ student: { id: string; fullName: string }; questionCount: number }[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const regeneratingCode = ref(false)

async function load() {
  loading.value = true
  error.value = null
  try {
    const [classesRes, rosterRes, strugglesRes, joinCodeRes, activityRes] = await Promise.all([
      teacherApi.getClasses(),
      teacherApi.getClassRoster(classId.value),
      teacherApi.getStruggles(classId.value),
      teacherApi.getClassJoinCode(classId.value),
      teacherApi.getActivity(),
    ])
    classInfo.value = classesRes.find((c) => c.class.id === classId.value) ?? null
    roster.value = rosterRes
    struggles.value = strugglesRes
    joinCode.value = joinCodeRes
    activity.value = activityRes.find((a) => a.class.id === classId.value)?.topStudents ?? []
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load this class.'
  } finally {
    loading.value = false
  }
}

watch(classId, load, { immediate: true })

function openStudent(studentId: string) {
  router.push(`/app/teacher/students/${studentId}`)
}

async function handleRegenerateJoinCode() {
  regeneratingCode.value = true
  try {
    joinCode.value = await teacherApi.regenerateClassJoinCode(classId.value)
  } finally {
    regeneratingCode.value = false
  }
}
</script>

<template>
  <div class="class-detail">
    <div v-if="loading" class="loading">Loading...</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <template v-else>
      <div class="header">
        <h1 class="page-title">{{ classInfo?.class.name ?? 'Class' }}</h1>
        <p v-if="classInfo && classInfo.subjects.length > 0" class="subtitle">
          {{ classInfo.subjects.map((s) => s.name).join(', ') }}
        </p>
      </div>

      <div class="code-card">
        <div class="code-card-header">
          <h2 class="section-title">Class join code</h2>
          <p class="section-subtitle">
            Share this code with students so they can join {{ classInfo?.class.name }}. Regenerating invalidates the old code immediately.
          </p>
        </div>
        <div class="code-row">
          <code v-if="joinCode" class="code-value">{{ joinCode.code }}</code>
          <span v-else class="code-empty">No code generated yet.</span>
          <button class="regen-btn" :disabled="regeneratingCode" @click="handleRegenerateJoinCode">
            {{ regeneratingCode ? 'Generating…' : (joinCode ? 'Regenerate' : 'Generate') }}
          </button>
        </div>
      </div>

      <div class="detail-grid">
        <StruggleTopicsCard :class-name="classInfo?.class.name ?? ''" :groups="struggles" />
        <MostActiveStudentsCard :students="activity" />
      </div>

      <ClassRosterCard :students="roster" @select="openStudent" />
    </template>
  </div>
</template>

<style scoped>
.class-detail {
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-width: 960px;
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

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
}

.code-card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
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
