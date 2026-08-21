<script setup lang="ts">
import { computed, ref } from 'vue'
import type { TeacherRosterStudent } from '../../../api/types'

const props = defineProps<{
  students: TeacherRosterStudent[]
}>()

const search = ref('')

const filteredStudents = computed(() => {
  const query = search.value.trim().toLowerCase()
  if (!query) return props.students
  return props.students.filter((s) => s.fullName.toLowerCase().includes(query))
})

function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

const AVATAR_CLASSES = ['avatar-0', 'avatar-1', 'avatar-2', 'avatar-3', 'avatar-4', 'avatar-5', 'avatar-6', 'avatar-7']

function avatarClass(id: string): string {
  let hash = 0
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) % AVATAR_CLASSES.length
  return AVATAR_CLASSES[Math.abs(hash) % AVATAR_CLASSES.length]
}
</script>

<template>
  <div class="card">
    <div class="card-header">
      <h3 class="card-title">Students · {{ students.length }}</h3>
      <input v-model="search" type="text" class="search-input" placeholder="Search students..." />
    </div>
    <div v-if="students.length === 0" class="empty">No students in this class yet.</div>
    <div v-else-if="filteredStudents.length === 0" class="empty">No students match "{{ search }}".</div>
    <div v-else class="student-grid">
      <div v-for="student in filteredStudents" :key="student.id" class="student-tile">
        <span class="avatar" :class="avatarClass(student.id)">{{ initials(student.fullName) }}</span>
        <span class="student-name">{{ student.fullName }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}

.card-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 16px;
  color: var(--ink);
}

.search-input {
  font-family: var(--font-body);
  font-size: 13px;
  color: var(--ink);
  background: var(--cream-2);
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 7px 12px;
  width: 200px;
}

.search-input:focus {
  outline: none;
  border-color: var(--green-br);
}

.empty {
  font-size: 13px;
  color: var(--muted);
}

.student-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px;
}

.student-tile {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: var(--cream-2);
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
}

.student-name {
  font-size: 14px;
  color: var(--ink-2);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.avatar {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border-radius: 50%;
  font-family: var(--font-heading);
  font-size: 12px;
  font-weight: 600;
}

.avatar-0 { background: #F6D9DF; color: #9C4A5C; }
.avatar-1 { background: #DCEBD0; color: #4F7A3C; }
.avatar-2 { background: #EFE1C0; color: #8A6A2C; }
.avatar-3 { background: #D9E6F2; color: #3E6E96; }
.avatar-4 { background: #E6DCF2; color: #6B4E96; }
.avatar-5 { background: #F6E3D0; color: #A06B33; }
.avatar-6 { background: #D3ECE6; color: #2E7566; }
.avatar-7 { background: #F2DCE0; color: #A15066; }
</style>
