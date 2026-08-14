<script setup lang="ts">
defineProps<{
  label: string
  type?: string
  modelValue: string
  placeholder?: string
}>()

defineEmits<{
  'update:modelValue': [value: string]
}>()
</script>

<template>
  <label class="field">
    <span class="field-label">{{ label }}</span>
    <input
      class="field-input"
      :type="type ?? 'text'"
      :value="modelValue"
      :placeholder="placeholder"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    />
  </label>
</template>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink-2);
}

.field-input {
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 10px 14px;
  font-size: 15px;
  color: var(--ink);
  background: var(--white);
  outline: none;
  transition: border-color 0.15s ease;
}

.field-input:focus {
  border-color: var(--green-br);
}
</style>
