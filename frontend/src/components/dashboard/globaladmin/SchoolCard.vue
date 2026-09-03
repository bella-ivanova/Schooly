<script setup lang="ts">
import { computed } from 'vue'
import type { SchoolSummary } from '../../../api/types'

const props = defineProps<{
  school: SchoolSummary
}>()

defineEmits<{ select: [] }>()

const createdLabel = computed(() =>
  new Date(props.school.createdAt).toLocaleDateString('en-US', { month: 'short', year: 'numeric' }),
)
</script>

<template>
  <button type="button" class="school-card" @click="$emit('select')">
    <span class="school-name">{{ school.name }}</span>
    <span class="school-created">Created {{ createdLabel }}</span>
    <span class="school-counts">{{ school.studentCount }} students · {{ school.teacherCount }} teachers</span>
  </button>
</template>

<style scoped>
.school-card {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  padding: 18px 20px;
  background: var(--card);
  border: 2px solid var(--line);
  border-radius: var(--r);
  cursor: pointer;
  text-align: left;
  font-family: inherit;
  transition: border-color 0.15s ease;
}

.school-card:hover {
  border-color: var(--green-br);
}

.school-name {
  font-family: var(--font-heading);
  font-size: 16px;
  font-weight: 600;
  color: var(--ink);
}

.school-created {
  font-size: 12px;
  color: var(--muted-2);
}

.school-counts {
  margin-top: 6px;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
