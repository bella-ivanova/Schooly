<script setup lang="ts">
import { ref } from 'vue'
import Field from '../shared/Field.vue'

defineProps<{
  isSubmitting: boolean
  errorMessages: string[]
}>()

const emit = defineEmits<{
  submit: [newPassword: string]
}>()

const newPassword = ref('')
const confirmPassword = ref('')
const mismatchError = ref<string | null>(null)

function handleSubmit() {
  if (newPassword.value !== confirmPassword.value) {
    mismatchError.value = "Passwords don't match."
    return
  }
  mismatchError.value = null
  emit('submit', newPassword.value)
}
</script>

<template>
  <form class="step-form" @submit.prevent="handleSubmit">
    <Field v-model="newPassword" label="New password" type="password" placeholder="••••••••" revealable />
    <Field
      v-model="confirmPassword"
      label="Confirm password"
      type="password"
      placeholder="••••••••"
      revealable
    />

    <p v-if="mismatchError" class="error-banner">{{ mismatchError }}</p>
    <ul v-else-if="errorMessages.length" class="error-banner">
      <li v-for="msg in errorMessages" :key="msg">{{ msg }}</li>
    </ul>

    <button type="submit" class="submit-btn" :disabled="isSubmitting">
      {{ isSubmitting ? 'Resetting…' : 'Reset password →' }}
    </button>
  </form>
</template>

<style scoped>
.step-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.error-banner {
  margin: 0;
  padding: 10px 14px 10px 14px;
  border-radius: var(--r-sm);
  background: var(--cream);
  color: var(--ink-2);
  font-size: 14px;
}

.submit-btn {
  border: none;
  border-radius: var(--r-lg);
  background: var(--green);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 16px;
  padding: 12px;
  cursor: pointer;
  transition: background-color 0.15s ease;
}

.submit-btn:hover:not(:disabled) {
  background: var(--green-br);
}

.submit-btn:disabled {
  opacity: 0.6;
  cursor: default;
}
</style>
