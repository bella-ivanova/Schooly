<script setup lang="ts">
import type { TeacherStudentSummary } from '../../../api/types'

defineProps<{
  student: TeacherStudentSummary
}>()

defineEmits<{
  select: []
}>()

const AVATAR_CLASSES = ['avatar-0', 'avatar-1', 'avatar-2', 'avatar-3', 'avatar-4', 'avatar-5', 'avatar-6', 'avatar-7']

function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

function avatarClass(id: string): string {
  let hash = 0
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) % AVATAR_CLASSES.length
  return AVATAR_CLASSES[Math.abs(hash) % AVATAR_CLASSES.length]
}
</script>

<template>
  <button type="button" class="student-card" @click="$emit('select')">
    <span class="avatar" :class="avatarClass(student.id)">{{ initials(student.fullName) }}</span>
    <span class="student-info">
      <span class="student-name">{{ student.fullName }}</span>
      <span class="student-meta">
        <template v-if="student.grade">Grade {{ student.grade }} · </template>{{ student.classNames.join(', ') }}
      </span>
    </span>
  </button>
</template>

<style scoped>
.student-card {
  display: flex;
  align-items: center;
  gap: 12px;
  text-align: left;
  padding: 16px 18px;
  background: var(--card);
  border: 2px solid var(--line);
  border-radius: var(--r);
  cursor: pointer;
  font-family: inherit;
}

.student-card:hover {
  border-color: var(--green-br);
}

.student-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.student-name {
  font-family: var(--font-heading);
  font-size: 16px;
  font-weight: 600;
  color: var(--ink);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.student-meta {
  font-size: 13px;
  color: var(--muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.avatar {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  font-family: var(--font-heading);
  font-size: 13px;
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
