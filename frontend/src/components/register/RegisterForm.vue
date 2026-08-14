<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { useRouter } from 'vue-router'
import type { ApiError } from '../../api/types'
import Field from '../shared/Field.vue'
import SelectField from '../shared/SelectField.vue'
import RoleTabs from './RoleTabs.vue'
import AuthShell from '../shared/AuthShell.vue'

const authStore = useAuthStore()
const router = useRouter()

const role = ref<'student' | 'teacher'>('student')
const username = ref('')
const email = ref('')
const fullName = ref('')
const password = ref('')
const grade = ref('')
const teacherRegistrationCode = ref('')
const agreedToTerms = ref(false)
const errorMessages = ref<string[]>([])
const isSubmitting = ref(false)

const gradeOptions = Array.from({ length: 12 }, (_, i) => ({
  value: String(i + 1),
  label: `Grade ${i + 1}`,
}))

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
    errorMessages.value = apiError.messages ?? ['Something went wrong. Please try again.']
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <AuthShell
    eyebrow="Create account"
    heading="Join Schooly"
    brand-heading="Start learning in minutes."
    brand-body="Set up your account and get grade-matched tutoring, practice, and mock exams — grounded in your own textbooks."
    brand-caption="Bulgarian curriculum · Grades 1–12"
  >
    <form class="register-form" @submit.prevent="handleSubmit">
      <RoleTabs v-model="role" />

      <div class="name-grade-row">
        <Field v-model="fullName" label="Full name" placeholder="Maria Koleva" />
        <SelectField
          v-if="role === 'student'"
          v-model="grade"
          label="Grade"
          placeholder="Select grade"
          :options="gradeOptions"
        />
      </div>

      <Field v-model="username" label="Username" placeholder="maria.k" />
      <Field v-model="email" label="Email" type="email" placeholder="maria.k@example.com" />
      <Field v-model="password" label="Password" type="password" placeholder="••••••••" revealable />
      <Field
        v-if="role === 'teacher'"
        v-model="teacherRegistrationCode"
        label="Teacher registration code"
        type="password"
        placeholder="Provided by your school"
      />

      <label class="terms-checkbox">
        <input type="checkbox" v-model="agreedToTerms" />
        <span>I agree to the <strong>Terms of Service</strong> and <strong>Privacy Policy</strong>.</span>
      </label>

      <ul v-if="errorMessages.length" class="error-banner">
        <li v-for="msg in errorMessages" :key="msg">{{ msg }}</li>
      </ul>

      <button type="submit" class="submit-btn" :disabled="isSubmitting || !agreedToTerms">
        {{ isSubmitting ? 'Creating account…' : 'Create account →' }}
      </button>

      <RouterLink to="/login" class="login-link">Already have an account? Sign in</RouterLink>
    </form>
  </AuthShell>
</template>

<style scoped>
.register-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.name-grade-row {
  display: flex;
  gap: 16px;
}

.name-grade-row > * {
  flex: 1;
  min-width: 0;
}

/* keep in sync with AuthShell.vue */
@media (max-width: 860px) {
  .name-grade-row {
    flex-direction: column;
  }
}

.terms-checkbox {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  font-size: 13px;
  color: var(--ink-2);
  cursor: pointer;
}

.terms-checkbox input {
  margin-top: 2px;
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
