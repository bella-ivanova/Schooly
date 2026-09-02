<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as teacherApi from '../../../api/teacher'
import type { TeacherStudentSummary } from '../../../api/types'
import StudentCard from './StudentCard.vue'
import SelectField from '../../shared/SelectField.vue'

const router = useRouter()
const students = ref<TeacherStudentSummary[]>([])
const loading = ref(true)
const classFilter = ref('')

const classOptions = computed(() => {
  const names = new Set<string>()
  for (const s of students.value) for (const name of s.classNames) names.add(name)
  return [
    { value: '', label: 'All classes' },
    ...[...names].sort((a, b) => a.localeCompare(b)).map((name) => ({ value: name, label: name })),
  ]
})

const filteredStudents = computed(() =>
  classFilter.value ? students.value.filter((s) => s.classNames.includes(classFilter.value)) : students.value,
)

function openStudent(studentId: string) {
  router.push(`/app/teacher/students/${studentId}`)
}

onMounted(async () => {
  students.value = await teacherApi.getAllStudents()
  loading.value = false
})
</script>

<template>
  <div class="teacher-students">
    <h1 class="page-title">Students</h1>

    <SelectField
      v-if="!loading && students.length > 0"
      label="Filter by class"
      :model-value="classFilter"
      :options="classOptions"
      @update:model-value="classFilter = $event"
    />

    <div v-if="loading" class="loading">Loading...</div>
    <p v-else-if="students.length === 0" class="empty">You don't have any students yet.</p>
    <p v-else-if="filteredStudents.length === 0" class="empty">No students in this class.</p>
    <div v-else class="student-grid">
      <StudentCard
        v-for="student in filteredStudents"
        :key="student.id"
        :student="student"
        @select="openStudent(student.id)"
      />
    </div>
  </div>
</template>

<style scoped>
.teacher-students {
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

.loading,
.empty {
  color: var(--muted);
  font-size: 14px;
}

.student-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
}
</style>
