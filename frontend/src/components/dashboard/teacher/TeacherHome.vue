<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import * as teacherApi from '../../../api/teacher'
import type { ClassJoinCode, TeacherActivityEntry, TeacherClassSummary, TeacherStruggleGroup } from '../../../api/types'
import ClassCard from './ClassCard.vue'
import StruggleTopicsCard from './StruggleTopicsCard.vue'
import MostActiveStudentsCard from './MostActiveStudentsCard.vue'

const classes = ref<TeacherClassSummary[]>([])
const activity = ref<TeacherActivityEntry[]>([])
const struggles = ref<TeacherStruggleGroup[]>([])
const selectedClassId = ref<number | null>(null)
const loading = ref(true)

const joinCode = ref<ClassJoinCode | null>(null)
const regeneratingCode = ref(false)

const selectedClass = computed(() => classes.value.find((c) => c.class.id === selectedClassId.value))

const selectedClassActivity = computed(
  () => activity.value.find((a) => a.class.id === selectedClassId.value)?.topStudents ?? [],
)

async function loadStruggles(classId: number) {
  struggles.value = await teacherApi.getStruggles(classId)
}

async function loadJoinCode(classId: number) {
  joinCode.value = await teacherApi.getClassJoinCode(classId)
}

function selectClass(classId: number) {
  selectedClassId.value = classId
}

async function handleRegenerateJoinCode() {
  if (selectedClassId.value === null) return
  regeneratingCode.value = true
  try {
    joinCode.value = await teacherApi.regenerateClassJoinCode(selectedClassId.value)
  } finally {
    regeneratingCode.value = false
  }
}

watch(selectedClassId, (id) => {
  if (id !== null) {
    loadStruggles(id)
    loadJoinCode(id)
  }
})

onMounted(async () => {
  const [classesRes, activityRes] = await Promise.all([teacherApi.getClasses(), teacherApi.getActivity()])
  classes.value = classesRes
  activity.value = activityRes
  if (classesRes.length > 0) selectedClassId.value = classesRes[0].class.id
  loading.value = false
})
</script>

<template>
  <div class="teacher-home">
    <h1 class="page-title">My Classes</h1>

    <div v-if="loading" class="loading">Loading...</div>
    <template v-else>
      <div class="class-grid">
        <ClassCard
          v-for="entry in classes"
          :key="entry.class.id"
          :entry="entry"
          :active="entry.class.id === selectedClassId"
          @select="selectClass(entry.class.id)"
        />
      </div>

      <div v-if="selectedClass" class="code-card">
        <div class="code-card-header">
          <h2 class="section-title">Class join code</h2>
          <p class="section-subtitle">Share this code with students so they can join {{ selectedClass.class.name }}. Regenerating invalidates the old code immediately.</p>
        </div>
        <div class="code-row">
          <code v-if="joinCode" class="code-value">{{ joinCode.code }}</code>
          <span v-else class="code-empty">No code generated yet.</span>
          <button class="regen-btn" :disabled="regeneratingCode" @click="handleRegenerateJoinCode">
            {{ regeneratingCode ? 'Generating…' : (joinCode ? 'Regenerate' : 'Generate') }}
          </button>
        </div>
      </div>

      <div v-if="selectedClass" class="detail-grid">
        <StruggleTopicsCard :class-name="selectedClass.class.name" :groups="struggles" />
        <MostActiveStudentsCard :students="selectedClassActivity" />
      </div>
    </template>
  </div>
</template>

<style scoped>
.teacher-home {
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-width: 960px;
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
  position: sticky;
  top: -40px;
  z-index: 2;
  padding: 4px 0 12px;
  background: var(--paper);
}

.loading {
  color: var(--muted);
  font-size: 14px;
}

.class-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 16px;
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
