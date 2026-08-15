<script setup lang="ts">
defineProps<{
  students: { student: { id: string; fullName: string }; questionCount: number }[]
}>()
</script>

<template>
  <div class="card">
    <h3 class="card-title">Most Active Students</h3>
    <div v-if="students.length === 0" class="empty">No activity in this period.</div>
    <div v-for="entry in students" :key="entry.student.id" class="student-row">
      <span class="student-name">{{ entry.student.fullName }}</span>
      <span class="badge">{{ entry.questionCount }} q's</span>
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
  gap: 12px;
}

.card-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 16px;
  color: var(--ink);
}

.empty {
  font-size: 13px;
  color: var(--muted);
}

.student-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.student-name {
  font-size: 14px;
  color: var(--ink-2);
}

.badge {
  font-size: 12px;
  font-weight: 600;
  color: var(--green-deep);
  background: var(--sage-soft);
  border-radius: 999px;
  padding: 3px 10px;
}
</style>
