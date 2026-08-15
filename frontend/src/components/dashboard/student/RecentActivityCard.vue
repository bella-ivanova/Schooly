<script setup lang="ts">
defineProps<{
  entries: { subject: string; topic: string }[]
}>()
</script>

<template>
  <div class="card">
    <h3 class="card-title">Recent Activity</h3>
    <div v-if="entries.length === 0" class="empty">No recent chat activity yet.</div>
    <div v-for="(entry, i) in entries" :key="i" class="activity-row">
      <span class="pill">{{ entry.subject }}</span>
      <span class="topic">{{ entry.topic }}</span>
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

.activity-row {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px 0;
  border-top: 1px solid var(--line);
}

.activity-row:first-of-type {
  border-top: none;
}

.pill {
  align-self: flex-start;
  font-size: 11px;
  font-weight: 600;
  color: var(--green-deep);
  background: var(--sage-soft);
  border-radius: 999px;
  padding: 2px 10px;
}

.topic {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink);
}
</style>
