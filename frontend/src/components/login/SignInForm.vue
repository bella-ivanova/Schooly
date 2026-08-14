<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { useRouter } from 'vue-router'
import type { ApiError } from '../../api/types'
import Field from '../shared/Field.vue'
import AuthShell from '../shared/AuthShell.vue'

const authStore = useAuthStore()
const router = useRouter()

const usernameOrEmail = ref('')
const password = ref('')
const errorMessage = ref<string | null>(null)
const isSubmitting = ref(false)

async function handleSubmit() {
  errorMessage.value = null
  isSubmitting.value = true
  try {
    await authStore.login(usernameOrEmail.value, password.value)
    await router.push('/app')
  } catch (err) {
    const apiError = err as ApiError
    errorMessage.value = apiError.message ?? 'Something went wrong. Please try again.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <AuthShell
    eyebrow="Welcome back"
    heading="Sign in to Schooly"
    brand-heading="Learn from your own textbooks."
    brand-body="A tutor that only teaches what's in your curriculum — grounded answers, practice, and mock exams for grades 1–12."
    brand-caption="Bulgarian curriculum · Grades 1–12"
  >
    <form class="sign-in-form" @submit.prevent="handleSubmit">
      <Field
        v-model="usernameOrEmail"
        label="Email or username"
        placeholder="maria.k"
      />
      <Field
        v-model="password"
        label="Password"
        type="password"
        placeholder="••••••••"
        revealable
      >
        <template #label-end>
          <RouterLink to="/forgot-password" class="forgot-link">Forgot?</RouterLink>
        </template>
      </Field>

      <p v-if="errorMessage" class="error-banner">{{ errorMessage }}</p>

      <button type="submit" class="submit-btn" :disabled="isSubmitting">
        {{ isSubmitting ? 'Signing in…' : 'Sign in →' }}
      </button>

      <RouterLink to="/register" class="register-link">New to Schooly? Create account</RouterLink>
    </form>
  </AuthShell>
</template>

<style scoped>
.sign-in-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.forgot-link {
  color: var(--green-deep);
  text-decoration: none;
}

.forgot-link:hover {
  text-decoration: underline;
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

.register-link {
  text-align: center;
  font-size: 14px;
  color: var(--green-deep);
  text-decoration: none;
}

.register-link:hover {
  text-decoration: underline;
}
</style>
