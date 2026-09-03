<script setup lang="ts">
defineProps<{
  title: string
  stats: { label: string; value: number }[]
}>()
</script>

<template>
  <div class="card">
    <h3 class="card-title">{{ title }}</h3>
    <div class="stat-row">
      <div v-for="stat in stats" :key="stat.label" class="stat">
        <span class="stat-value">{{ stat.value }}</span>
        <span class="stat-label">{{ stat.label }}</span>
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
</style>
