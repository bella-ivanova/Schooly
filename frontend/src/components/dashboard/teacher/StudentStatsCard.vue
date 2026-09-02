<script setup lang="ts">
import type { TeacherStudentStats } from '../../../api/types'

defineProps<{
  stats: TeacherStudentStats
}>()

function formatLastActive(iso: string | null): string {
  if (!iso) return 'Never'
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="card">
    <h3 class="card-title">Activity</h3>
    <div class="stat-row">
      <div class="stat">
        <span class="stat-value">{{ stats.questionCount30d }}</span>
        <span class="stat-label">Questions (30d)</span>
      </div>
      <div class="stat">
        <span class="stat-value">{{ formatLastActive(stats.lastActiveAt) }}</span>
        <span class="stat-label">Last active</span>
      </div>
      <div class="stat">
        <span class="stat-value">{{ stats.savedExamCount }}</span>
        <span class="stat-label">Saved exams</span>
      </div>
    </div>

    <div class="weak-spots">
      <h4 class="subsection-title">Weak spots (30d)</h4>
      <div v-if="stats.weakSpots.length === 0" class="empty">No weak spots detected yet.</div>
      <div v-else class="pill-row">
        <span v-for="spot in stats.weakSpots" :key="spot.topic" class="pill">{{ spot.topic }} ×{{ spot.count }}</span>
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

.card-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 16px;
  color: var(--ink);
}

.stat-row {
  display: flex;
  gap: 24px;
  flex-wrap: wrap;
}

.stat {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat-value {
  font-family: var(--font-heading);
  font-size: 22px;
  font-weight: 600;
  color: var(--ink);
}

.stat-label {
  font-size: 12px;
  color: var(--muted);
}

.weak-spots {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.subsection-title {
  margin: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--ink-2);
}

.empty {
  font-size: 13px;
  color: var(--muted);
}

.pill-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.pill {
  font-size: 12px;
  font-weight: 600;
  color: var(--green-deep);
  background: var(--sage-soft);
  border-radius: 999px;
  padding: 4px 12px;
}
</style>
