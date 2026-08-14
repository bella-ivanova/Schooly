<script setup lang="ts">
// Functional here (unlike the login screen): RegisterRequest.role is required by the backend.
defineProps<{
  modelValue: 'student' | 'teacher'
}>()

defineEmits<{
  'update:modelValue': [value: 'student' | 'teacher']
}>()
</script>

<template>
  <div class="role-tabs">
    <button
      type="button"
      class="role-tab"
      :class="{ active: modelValue === 'student' }"
      @click="$emit('update:modelValue', 'student')"
    >
      Ученик
    </button>
    <button
      type="button"
      class="role-tab"
      :class="{ active: modelValue === 'teacher' }"
      @click="$emit('update:modelValue', 'teacher')"
    >
      Учител
    </button>
  </div>
</template>

<style scoped>
.role-tabs {
  display: flex;
  gap: 6px;
  background: var(--sage-soft);
  border-radius: var(--r-lg);
  padding: 4px;
}

.role-tab {
  flex: 1;
  border: none;
  background: transparent;
  color: var(--ink-2);
  font-family: var(--font-heading);
  font-size: 14px;
  padding: 8px 12px;
  border-radius: var(--r);
  cursor: pointer;
  transition: background-color 0.15s ease, color 0.15s ease;
}

.role-tab.active {
  background: var(--green-br);
  color: var(--white);
}
</style>
