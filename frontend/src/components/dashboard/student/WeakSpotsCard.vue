<script setup lang="ts">
import type { WeakSpot } from '../../../api/types'

defineProps<{
  weakSpots: WeakSpot[]
}>()
</script>

<template>
  <div class="card">
    <h3 class="card-title">Weak Spots (7d)</h3>
    <div v-if="weakSpots.length === 0" class="empty">No weak spots detected yet.</div>
    <div v-else class="pill-row">
      <span v-for="spot in weakSpots" :key="spot.topic" class="pill">{{ spot.topic }} ×{{ spot.count }}</span>
    </div>

    <router-link to="/app/student/exams" class="cta">Generate Practice Exam</router-link>
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
  gap: 14px;
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

.cta {
  display: inline-block;
  text-align: center;
  text-decoration: none;
  border: none;
  border-radius: var(--r-sm);
  background: var(--green);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 14px;
  font-weight: 600;
  padding: 12px 16px;
  cursor: pointer;
}

.cta:hover {
  background: var(--green-deep);
}
</style>
