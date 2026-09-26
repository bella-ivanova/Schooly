<script setup lang="ts">
import type { TeacherClassSummary } from '../../../api/types'

defineProps<{
  entry: TeacherClassSummary
  active: boolean
}>()

defineEmits<{
  select: []
}>()
</script>

<template>
  <button type="button" class="class-card" :class="{ active }" @click="$emit('select')">
    <span class="class-name">{{ entry.class.name }}</span>
    <span class="class-meta">
      {{ entry.subjects.map((s) => s.name).join(', ') }} · {{ entry.studentCount }} students
    </span>
  </button>
</template>

<style scoped>
.class-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  text-align: left;
  padding: 16px 18px;
  background: var(--card);
  border: 2px solid var(--line);
  border-radius: var(--r);
  cursor: pointer;
  font-family: inherit;
}

.class-card.active {
  border-color: var(--green-br);
}

.class-name {
  font-family: var(--font-heading);
  font-size: 16px;
  font-weight: 600;
  color: var(--ink);
}

.class-meta {
  font-size: 13px;
  color: var(--muted);
}

/* feel: lift on desktop hover, press on every device */
.class-card {
  transition:
    border-color var(--dur-fast) ease,
    box-shadow var(--dur-fast) ease,
    transform var(--dur-press) var(--ease-out);
}

@media (hover: hover) and (pointer: fine) {
  .class-card:hover {
    border-color: var(--green-br);
    transform: translateY(-2px);
    box-shadow: var(--shadow);
  }
}

.class-card:active {
  transform: scale(0.98);
}
</style>
