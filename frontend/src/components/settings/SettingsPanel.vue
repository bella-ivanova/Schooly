<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import * as authApi from '../../api/auth'
import * as teacherApi from '../../api/teacher'
import type { ApiError } from '../../api/types'
import Field from '../shared/Field.vue'
import SelectField from '../shared/SelectField.vue'

const authStore = useAuthStore()

const roleLabels: Record<string, string> = {
  student: 'Student',
  teacher: 'Teacher',
  schooladmin: 'School Admin',
  admin: 'Global Admin',
}
const roleLabel = computed(() => roleLabels[authStore.role ?? ''] ?? authStore.role ?? '')

const subjects = ref<string[]>([])
const subjectsLoading = ref(false)
const subjectsError = ref(false)

onMounted(async () => {
  if (authStore.role !== 'teacher') return
  subjectsLoading.value = true
  try {
    subjects.value = await teacherApi.getMySubjects()
  } catch {
    subjectsError.value = true
  } finally {
    subjectsLoading.value = false
  }
})

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
    <div class="page-header">
      <h1 class="page-title">Settings</h1>
      <p class="page-subtitle">Manage your display name, password, and account details.</p>
    </div>

    <section class="settings-card">
      <div class="card-header">
        <div>
          <h2 class="section-title">Account information</h2>
          <p class="card-subtitle">Managed by your school. Contact an admin to request a change.</p>
        </div>
        <span class="readonly-pill">Read only</span>
      </div>
      <div class="info-row">
        <div class="info-field">
          <span class="info-label">Username</span>
          <span class="info-value">{{ authStore.user?.username }}</span>
        </div>
        <div class="info-field">
          <span class="info-label">Email</span>
          <span class="info-value">{{ authStore.user?.email }}</span>
        </div>
        <div class="info-field">
          <span class="info-label">Role</span>
          <span class="info-value">{{ roleLabel }}</span>
        </div>
        <div class="info-field">
          <span class="info-label">School</span>
          <span class="info-value">{{ authStore.user?.schoolName ?? '—' }}</span>
        </div>
        <div v-if="authStore.role === 'student'" class="info-field">
          <span class="info-label">Class letter</span>
          <span class="info-value">{{ authStore.user?.classLetter ?? '—' }}</span>
        </div>
        <div v-if="authStore.role === 'teacher'" class="info-field">
          <span class="info-label">Subjects taught</span>
          <span class="info-value">
            <template v-if="subjectsLoading">Loading…</template>
            <template v-else-if="subjectsError">Could not load subjects.</template>
            <template v-else-if="!subjects.length">No subjects assigned yet.</template>
            <template v-else>{{ subjects.join(', ') }}</template>
          </span>
        </div>
      </div>
    </section>

    <form class="settings-card" @submit.prevent="handleProfileSubmit">
      <div class="card-header">
        <div>
          <h2 class="section-title">Display name</h2>
          <p class="card-subtitle">This is the name students and staff see across Schooly.</p>
        </div>
      </div>
      <div class="name-grade-row">
        <Field v-model="fullName" placeholder="Your name" class="name-field" />
        <SelectField
          v-if="authStore.role === 'student'"
          v-model="grade"
          label="Grade"
          placeholder="Select grade"
          :options="gradeOptions"
        />
        <button type="submit" class="submit-btn" :disabled="profileSaving">
          {{ profileSaving ? 'Saving…' : 'Save changes' }}
        </button>
      </div>
      <p v-if="profileMessage" class="success-banner">{{ profileMessage }}</p>
      <ul v-if="profileErrors.length" class="error-banner">
        <li v-for="msg in profileErrors" :key="msg">{{ msg }}</li>
      </ul>
    </form>

    <form class="settings-card" @submit.prevent="handlePasswordSubmit">
      <div class="card-header">
        <div>
          <h2 class="section-title">Change password</h2>
          <p class="card-subtitle">Use at least 8 characters. You'll stay signed in on this device.</p>
        </div>
      </div>
      <div class="password-row">
        <Field v-model="currentPassword" label="Current password" type="password" placeholder="••••••••" revealable />
        <Field v-model="newPassword" label="New password" type="password" placeholder="••••••••" revealable />
        <Field v-model="confirmPassword" label="Confirm new password" type="password" placeholder="••••••••" revealable />
      </div>
      <p v-if="passwordMessage" class="success-banner">{{ passwordMessage }}</p>
      <ul v-if="passwordErrors.length" class="error-banner">
        <li v-for="msg in passwordErrors" :key="msg">{{ msg }}</li>
      </ul>
      <button type="submit" class="submit-btn" :disabled="passwordSaving">
        {{ passwordSaving ? 'Changing…' : 'Update password' }}
      </button>
    </form>
  </div>
</template>

<style scoped>
.settings-panel {
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-width: 1400px;
}

.page-header {
  position: sticky;
  top: -40px;
  z-index: 2;
  padding: 4px 0 12px;
  background: var(--paper);
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
}

.page-subtitle {
  margin: 6px 0 0;
  font-size: 15px;
  color: var(--muted);
}

.settings-card {
  display: flex;
  flex-direction: column;
  gap: 20px;
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 24px;
}

.card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.section-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 20px;
  color: var(--ink);
}

.card-subtitle {
  margin: 4px 0 0;
  font-size: 14px;
  color: var(--muted);
}

.readonly-pill {
  flex-shrink: 0;
  border-radius: var(--r-lg);
  background: var(--sage-soft);
  color: var(--green-deep);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  padding: 6px 14px;
  white-space: nowrap;
}

.info-row {
  display: flex;
  flex-wrap: wrap;
  gap: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--line);
}

.info-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  flex: 1 1 140px;
  min-width: 0;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--line);
}

.info-label {
  font-size: 13px;
  color: var(--muted);
}

.info-value {
  font-size: 15px;
  font-weight: 600;
  color: var(--ink);
  overflow-wrap: break-word;
}

.name-grade-row {
  display: flex;
  align-items: flex-end;
  gap: 16px;
}

.name-grade-row > .name-field {
  flex: 2;
  min-width: 0;
}

.name-grade-row > :not(.name-field) {
  flex: 1;
  min-width: 0;
}

.name-grade-row > .submit-btn {
  flex: 0 0 auto;
  align-self: flex-end;
}

.password-row {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
}

.password-row > * {
  flex: 1;
  min-width: 200px;
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
