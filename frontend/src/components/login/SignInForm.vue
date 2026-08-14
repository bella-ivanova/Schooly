<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { useRouter } from 'vue-router'
import type { ApiError } from '../../api/types'
import Field from '../shared/Field.vue'

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
    errorMessage.value = apiError.message ?? 'Възникна грешка. Опитайте отново.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <form class="sign-in-card" @submit.prevent="handleSubmit">
    <h1 class="title">Здравей отново</h1>
    <p class="subtitle">Влез в своя акаунт в Schooly</p>

    <Field
      v-model="usernameOrEmail"
      label="Потребител или имейл"
      placeholder="ivan.ivanov"
    />
    <Field
      v-model="password"
      label="Парола"
      type="password"
      placeholder="••••••••"
    />

    <p v-if="errorMessage" class="error-banner">{{ errorMessage }}</p>

    <button type="submit" class="submit-btn" :disabled="isSubmitting">
      {{ isSubmitting ? 'Влизане…' : 'Влез' }}
    </button>

    <RouterLink to="/register" class="register-link">Нямаш акаунт? Регистрирай се</RouterLink>
  </form>
</template>

<style scoped>
.sign-in-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: var(--card);
  border-radius: var(--r-xl);
  box-shadow: var(--shadow-lg);
  padding: 36px;
  width: 100%;
  max-width: 380px;
}

.title {
  font-size: 26px;
  color: var(--ink);
}

.subtitle {
  margin: -8px 0 4px;
  color: var(--muted);
  font-size: 14px;
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
