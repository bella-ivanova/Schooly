<script setup lang="ts">
import { ref, computed } from 'vue'

defineOptions({ inheritAttrs: false })

const props = defineProps<{
  label: string
  type?: string
  modelValue: string
  placeholder?: string
  revealable?: boolean
}>()

defineEmits<{
  'update:modelValue': [value: string]
}>()

const isRevealed = ref(false)

const effectiveType = computed(() => {
  if (props.revealable) {
    return isRevealed.value ? 'text' : (props.type ?? 'password')
  }
  return props.type ?? 'text'
})
</script>

<template>
  <label class="field">
    <span class="field-label-row">
      <span class="field-label">{{ label }}</span>
      <span class="field-label-end"><slot name="label-end" /></span>
    </span>
    <span class="field-input-wrap">
      <input
        class="field-input"
        :class="{ 'has-reveal': revealable }"
        :type="effectiveType"
        :value="modelValue"
        :placeholder="placeholder"
        v-bind="$attrs"
        @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      />
      <button
        v-if="revealable"
        type="button"
        class="reveal-toggle"
        @click="isRevealed = !isRevealed"
      >
        {{ isRevealed ? 'Hide' : 'Show' }}
      </button>
    </span>
  </label>
</template>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
}

.field-label {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink-2);
}

.field-label-end {
  font-size: 13px;
}

.field-label-end:empty {
  display: none;
}

.field-input-wrap {
  position: relative;
  display: flex;
}

.field-input {
  flex: 1;
  min-width: 0;
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

.field-input.has-reveal {
  padding-right: 54px;
}

.reveal-toggle {
  position: absolute;
  right: 14px;
  top: 50%;
  transform: translateY(-50%);
  border: none;
  background: transparent;
  color: var(--green-deep);
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
}
</style>
