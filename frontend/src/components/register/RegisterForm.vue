<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { useRouter } from 'vue-router'
import type { ApiError } from '../../api/types'
import Field from '../shared/Field.vue'
import RoleTabs from './RoleTabs.vue'

const authStore = useAuthStore()
const router = useRouter()

const role = ref<'student' | 'teacher'>('student')
const username = ref('')
const email = ref('')
const fullName = ref('')
const password = ref('')
const grade = ref('')
const teacherRegistrationCode = ref('')
const errorMessages = ref<string[]>([])
const isSubmitting = ref(false)

async function handleSubmit() {
  errorMessages.value = []
  isSubmitting.value = true
  try {
    await authStore.register({
      username: username.value,
      email: email.value,
      fullName: fullName.value,
      password: password.value,
      role: role.value,
      grade: grade.value ? Number(grade.value) : undefined,
      teacherRegistrationCode: role.value === 'teacher' ? teacherRegistrationCode.value : undefined,
    })
    await router.push('/app')
  } catch (err) {
    const apiError = err as ApiError
    errorMessages.value = apiError.messages ?? ['Възникна грешка. Опитайте отново.']
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <form class="register-card" @submit.prevent="handleSubmit">
    <h1 class="title">Създай акаунт</h1>
    <p class="subtitle">Регистрирай се в Schooly</p>

    <RoleTabs v-model="role" />

    <Field v-model="fullName" label="Име и фамилия" placeholder="Иван Иванов" />
    <Field v-model="username" label="Потребителско име" placeholder="ivan.ivanov" />
    <Field v-model="email" label="Имейл" type="email" placeholder="ivan@example.com" />
    <Field v-model="password" label="Парола" type="password" placeholder="••••••••" />
    <Field
      v-if="role === 'student'"
      v-model="grade"
      label="Клас (по избор)"
      type="number"
      placeholder="8"
    />
    <Field
      v-if="role === 'teacher'"
      v-model="teacherRegistrationCode"
      label="Код за учителска регистрация"
      type="password"
      placeholder="Получен от училището"
    />

    <ul v-if="errorMessages.length" class="error-banner">
      <li v-for="msg in errorMessages" :key="msg">{{ msg }}</li>
    </ul>

    <button type="submit" class="submit-btn" :disabled="isSubmitting">
      {{ isSubmitting ? 'Регистрация…' : 'Регистрирай се' }}
    </button>

    <RouterLink to="/login" class="login-link">Вече имаш акаунт? Влез</RouterLink>
  </form>
</template>

<style scoped>
.register-card {
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
  padding: 10px 14px 10px 28px;
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

.login-link {
  text-align: center;
  font-size: 14px;
  color: var(--green-deep);
  text-decoration: none;
}

.login-link:hover {
  text-decoration: underline;
}
</style>
