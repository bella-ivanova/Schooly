<script setup lang="ts">
import { computed } from 'vue'
import type { TeacherStruggleGroup } from '../../../api/types'

const props = defineProps<{
  className: string
  groups: TeacherStruggleGroup[]
}>()

const topEntries = computed(() =>
  props.groups.flatMap((g) => g.topTopics.map((t) => ({ ...t, subject: g.subject.name }))),
)

const maxCount = computed(() => Math.max(1, ...topEntries.value.map((e) => e.count)))
</script>

<template>
  <div class="card">
    <h3 class="card-title">Struggle Topics · {{ className }}</h3>
    <div v-if="topEntries.length === 0" class="empty">No struggle topics in this period.</div>
    <div v-for="entry in topEntries" :key="`${entry.subject}-${entry.topic}`" class="topic-row">
      <span class="topic-label">{{ entry.topic }} · {{ entry.subject }} · {{ entry.count }}</span>
      <div class="bar-track">
        <div class="bar-fill" :style="{ width: `${(entry.count / maxCount) * 100}%` }" />
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

.topic-row {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.topic-label {
  font-size: 13px;
  color: var(--ink-2);
}

.bar-track {
  height: 8px;
  border-radius: 4px;
  background: var(--sage-soft);
  overflow: hidden;
}

.bar-fill {
  height: 100%;
  background: var(--green-br);
  border-radius: 4px;
}
</style>
