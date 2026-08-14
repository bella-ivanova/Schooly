<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import * as authApi from '../../api/auth'
import type { ApiError } from '../../api/types'
import AuthShell from '../shared/AuthShell.vue'
import EmailStep from './EmailStep.vue'
import CodeStep from './CodeStep.vue'
import NewPasswordStep from './NewPasswordStep.vue'

const router = useRouter()

const step = ref<'email' | 'code' | 'password'>('email')
const email = ref('')
const verifiedCode = ref('')
const errorMessage = ref<string | null>(null)
const errorMessages = ref<string[]>([])
const isSubmitting = ref(false)

const stepCopy = computed(() => {
  switch (step.value) {
    case 'email':
      return {
        eyebrow: 'Forgot password',
        heading: 'Reset your password',
        subtitle: "Enter the email on your account and we'll send you a reset code.",
      }
    case 'code':
      return {
        eyebrow: 'Forgot password',
        heading: 'Check your email',
        subtitle: `Enter the 6-digit code we sent to ${email.value}.`,
      }
    case 'password':
      return {
        eyebrow: 'Forgot password',
        heading: 'Choose a new password',
        subtitle: 'Pick a new password for your account.',
      }
  }
})

async function submitEmail(value: string) {
  errorMessage.value = null
  isSubmitting.value = true
  try {
    await authApi.forgotPassword({ email: value })
    email.value = value
    step.value = 'code'
  } catch (err) {
    const apiError = err as ApiError
    if (apiError.status === 404) {
      errorMessage.value = 'No account found with that email.'
    } else if (apiError.status === 429) {
      errorMessage.value = 'A reset code was already requested recently. Please wait before trying again.'
    } else {
      errorMessage.value = apiError.message ?? 'Something went wrong. Please try again.'
    }
  } finally {
    isSubmitting.value = false
  }
}

async function submitCode(code: string) {
  errorMessage.value = null
  isSubmitting.value = true
  try {
    await authApi.verifyResetCode({ email: email.value, code })
    verifiedCode.value = code
    step.value = 'password'
  } catch (err) {
    const apiError = err as ApiError
    if (apiError.status === 429) {
      errorMessage.value = 'Too many incorrect attempts. Please request a new code.'
    } else {
      errorMessage.value = apiError.message ?? 'Invalid or expired code.'
    }
  } finally {
    isSubmitting.value = false
  }
}

async function resendCode() {
  await submitEmail(email.value)
}

async function submitNewPassword(newPassword: string) {
  errorMessages.value = []
  isSubmitting.value = true
  try {
    await authApi.resetPassword({ email: email.value, code: verifiedCode.value, newPassword })
    await router.push({ name: 'login' })
  } catch (err) {
    const apiError = err as ApiError
    if (apiError.status === 429) {
      errorMessages.value = ['Too many incorrect attempts. Please request a new code.']
    } else {
      errorMessages.value = apiError.messages ?? [apiError.message ?? 'Something went wrong. Please try again.']
    }
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <AuthShell
    :eyebrow="stepCopy.eyebrow"
    :heading="stepCopy.heading"
    :subtitle="stepCopy.subtitle"
    brand-heading="Learn from your own textbooks."
    brand-body="A tutor that only teaches what's in your curriculum — grounded answers, practice, and mock exams for grades 1–12."
    brand-caption="Bulgarian curriculum · Grades 1–12"
  >
    <EmailStep
      v-if="step === 'email'"
      :is-submitting="isSubmitting"
      :error-message="errorMessage"
      @submit="submitEmail"
    />
    <CodeStep
      v-else-if="step === 'code'"
      :is-submitting="isSubmitting"
      :error-message="errorMessage"
      @submit="submitCode"
      @resend="resendCode"
    />
    <NewPasswordStep
      v-else
      :is-submitting="isSubmitting"
      :error-messages="errorMessages"
      @submit="submitNewPassword"
    />

    <RouterLink to="/login" class="back-link">Back to sign in</RouterLink>
  </AuthShell>
</template>

<style scoped>
.back-link {
  display: block;
  text-align: center;
  font-size: 14px;
  color: var(--green-deep);
  text-decoration: none;
  margin-top: 12px;
}

.back-link:hover {
  text-decoration: underline;
}
</style>
