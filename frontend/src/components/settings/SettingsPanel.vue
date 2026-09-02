<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import * as authApi from '../../api/auth'
import type { ApiError } from '../../api/types'
import Field from '../shared/Field.vue'
import SelectField from '../shared/SelectField.vue'

const authStore = useAuthStore()

const fullName = ref(authStore.user?.fullName ?? '')
const grade = ref(authStore.user?.grade ? String(authStore.user.grade) : '')
const profileSaving = ref(false)
const profileMessage = ref<string | null>(null)
const profileErrors = ref<string[]>([])

const gradeOptions = Array.from({ length: 12 }, (_, i) => ({
  value: String(i + 1),
  label: `Grade ${i + 1}`,
}))

async function handleProfileSubmit() {
  profileSaving.value = true
  profileMessage.value = null
  profileErrors.value = []
  try {
    const updated = await authApi.updateProfile({
      fullName: fullName.value,
      grade: authStore.role === 'student' && grade.value ? Number(grade.value) : undefined,
    })
    authStore.updateUser(updated)
    profileMessage.value = 'Profile updated.'
  } catch (err) {
    const apiError = err as ApiError
    profileErrors.value = apiError.messages ?? ['Could not update profile.']
  } finally {
    profileSaving.value = false
  }
}

const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const passwordSaving = ref(false)
const passwordMessage = ref<string | null>(null)
const passwordErrors = ref<string[]>([])

async function handlePasswordSubmit() {
  passwordMessage.value = null
  passwordErrors.value = []
  if (newPassword.value !== confirmPassword.value) {
    passwordErrors.value = ["New passwords don't match."]
    return
  }
  passwordSaving.value = true
  try {
    await authApi.changePassword({
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    })
    passwordMessage.value = 'Password changed.'
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
  } catch (err) {
    const apiError = err as ApiError
    passwordErrors.value = apiError.messages ?? ['Could not change password.']
  } finally {
    passwordSaving.value = false
  }
}
</script>

<template>
  <div class="settings-panel">
    <h1 class="page-title">Settings</h1>

    <form class="settings-card" @submit.prevent="handleProfileSubmit">
      <h2 class="section-title">Profile</h2>
      <div class="name-grade-row">
        <Field v-model="fullName" label="Full name" placeholder="Your name" />
        <SelectField
          v-if="authStore.role === 'student'"
          v-model="grade"
          label="Grade"
          placeholder="Select grade"
          :options="gradeOptions"
        />
      </div>
      <p v-if="profileMessage" class="success-banner">{{ profileMessage }}</p>
      <ul v-if="profileErrors.length" class="error-banner">
        <li v-for="msg in profileErrors" :key="msg">{{ msg }}</li>
      </ul>
      <button type="submit" class="submit-btn" :disabled="profileSaving">
        {{ profileSaving ? 'Saving…' : 'Save profile' }}
      </button>
    </form>

    <form class="settings-card" @submit.prevent="handlePasswordSubmit">
      <h2 class="section-title">Change password</h2>
      <Field v-model="currentPassword" label="Current password" type="password" placeholder="••••••••" revealable />
      <Field v-model="newPassword" label="New password" type="password" placeholder="••••••••" revealable />
      <Field v-model="confirmPassword" label="Confirm new password" type="password" placeholder="••••••••" revealable />
      <p v-if="passwordMessage" class="success-banner">{{ passwordMessage }}</p>
      <ul v-if="passwordErrors.length" class="error-banner">
        <li v-for="msg in passwordErrors" :key="msg">{{ msg }}</li>
      </ul>
      <button type="submit" class="submit-btn" :disabled="passwordSaving">
        {{ passwordSaving ? 'Changing…' : 'Change password' }}
      </button>
    </form>
  </div>
</template>

<style scoped>
.settings-panel {
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-width: 560px;
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
  position: sticky;
  top: -40px;
  z-index: 2;
  padding: 4px 0 12px;
  background: var(--paper);
}

.settings-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 20px;
}

.section-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 18px;
  color: var(--ink);
}

.name-grade-row {
  display: flex;
  gap: 16px;
}

.name-grade-row > * {
  flex: 1;
  min-width: 0;
}

.success-banner {
  margin: 0;
  padding: 10px 14px;
  border-radius: var(--r-sm);
  background: var(--cream-2);
  color: var(--ink-2);
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
  align-self: flex-start;
  border: none;
  border-radius: var(--r-lg);
  background: var(--green);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 15px;
  padding: 10px 20px;
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
