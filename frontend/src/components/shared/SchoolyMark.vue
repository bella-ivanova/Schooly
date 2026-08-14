<script setup lang="ts">
withDefaults(
  defineProps<{
    size?: number
    variant?: 'solid' | 'ghost'
  }>(),
  {
    size: 40,
    variant: 'solid',
  },
)
</script>

<template>
  <svg
    v-if="variant === 'solid'"
    :width="size"
    :height="size"
    viewBox="0 0 40 40"
    fill="none"
    xmlns="http://www.w3.org/2000/svg"
    class="schooly-mark"
  >
    <rect width="40" height="40" rx="12" fill="var(--green-br)" />
    <circle cx="23" cy="17" r="9" fill="var(--white)" />
    <circle cx="23" cy="17" r="3" fill="var(--green-br)" />
  </svg>
  <svg
    v-else
    :width="size"
    :height="size"
    viewBox="0 0 40 40"
    fill="none"
    xmlns="http://www.w3.org/2000/svg"
    class="schooly-mark schooly-mark-ghost"
  >
    <circle cx="20" cy="20" r="20" fill="rgba(255, 255, 255, 0.18)" />
    <circle cx="20" cy="20" r="12" fill="rgba(255, 255, 255, 0.35)" />
  </svg>
</template>

<style scoped>
.schooly-mark {
  flex-shrink: 0;
  display: block;
}
</style>
