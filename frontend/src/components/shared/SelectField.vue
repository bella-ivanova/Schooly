<script setup lang="ts">
defineProps<{
  label: string
  modelValue: string
  options: { value: string; label: string }[]
  placeholder?: string
}>()

defineEmits<{
  'update:modelValue': [value: string]
}>()
</script>

<template>
  <label class="field">
    <span class="field-label">{{ label }}</span>
    <span class="field-input-wrap">
      <select
        class="field-input"
        :value="modelValue"
        @change="$emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
      >
        <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
        <option v-for="opt in options" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
      </select>
      <span class="caret">▾</span>
    </span>
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

.field-input-wrap {
  position: relative;
  display: flex;
}

.field-input {
  flex: 1;
  min-width: 0;
  appearance: none;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 10px 36px 10px 14px;
  font-size: 15px;
  font-family: inherit;
  color: var(--ink);
  background: var(--white);
  outline: none;
  transition: border-color 0.15s ease;
}

.field-input:focus {
  border-color: var(--green-br);
}

.caret {
  position: absolute;
  right: 14px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--muted);
  pointer-events: none;
  font-size: 12px;
}
</style>
