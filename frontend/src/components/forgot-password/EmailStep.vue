<script setup lang="ts">
import { ref } from 'vue'
import Field from '../shared/Field.vue'

defineProps<{
  isSubmitting: boolean
  errorMessage: string | null
}>()

const emit = defineEmits<{
  submit: [email: string]
}>()

const email = ref('')

function handleSubmit() {
  emit('submit', email.value)
}
</script>

<template>
  <form class="step-form" @submit.prevent="handleSubmit">
    <Field v-model="email" label="Email" type="email" placeholder="maria.k@example.com" />

    <p v-if="errorMessage" class="error-banner">{{ errorMessage }}</p>

    <button type="submit" class="submit-btn" :disabled="isSubmitting">
      {{ isSubmitting ? 'Sending…' : 'Send reset code →' }}
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
  padding: 10px 14px;
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
