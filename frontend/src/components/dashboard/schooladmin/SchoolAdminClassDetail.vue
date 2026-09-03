<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as schoolAdminApi from '../../../api/schoolAdmin'
import type { AdminClassDetail, AdminSubjectSummary, AdminUserSummary } from '../../../api/types'
import Field from '../../shared/Field.vue'
import SelectField from '../../shared/SelectField.vue'
import ClassRosterEditor from './ClassRosterEditor.vue'
import ClassTeacherAssignmentsEditor from './ClassTeacherAssignmentsEditor.vue'

const route = useRoute()
const router = useRouter()
const classId = computed(() => Number(route.params.classId))

const detail = ref<AdminClassDetail | null>(null)
const users = ref<AdminUserSummary[]>([])
const subjects = ref<AdminSubjectSummary[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

const nameInput = ref('')
const subjectIdInput = ref('')
const gradeInput = ref('')
const saving = ref(false)
const saveError = ref<string | null>(null)

const gradeOptions = Array.from({ length: 12 }, (_, i) => ({
  value: String(i + 1),
  label: `Grade ${i + 1}`,
}))

const homeroomTeacherIdInput = ref('')
const settingHomeroom = ref(false)
const homeroomError = ref<string | null>(null)

const teacherOptions = computed(() =>
  users.value
    .filter((u) => u.role === 'Teacher')
    .map((t) => ({ value: t.id, label: t.fullName || t.username })),
)

const subjectOptions = computed(() =>
  subjects.value.map((s) => ({ value: String(s.id), label: s.name })),
)

async function loadDetail() {
  detail.value = await schoolAdminApi.getClassDetail(classId.value)
  nameInput.value = detail.value.name
  subjectIdInput.value = detail.value.subjectId != null ? String(detail.value.subjectId) : ''
  gradeInput.value = detail.value.grade != null ? String(detail.value.grade) : ''
  const matched = teacherOptions.value.find((t) => t.label === detail.value?.homeroomTeacherUsername)
  homeroomTeacherIdInput.value = matched?.value ?? ''
}

async function loadAll() {
  loading.value = true
  loadError.value = null
  try {
    const [usersRes, subjectsRes] = await Promise.all([schoolAdminApi.getUsers(), schoolAdminApi.getSubjects()])
    users.value = usersRes
    subjects.value = subjectsRes
    await loadDetail()
  } catch (err) {
    loadError.value = (err as { message?: string }).message ?? 'Could not load class.'
  } finally {
    loading.value = false
  }
}

async function reload() {
  await loadDetail()
}

async function handleSave() {
  if (!nameInput.value.trim() || !subjectIdInput.value) return
  saveError.value = null
  saving.value = true
  try {
    await schoolAdminApi.updateClass(
      classId.value,
      nameInput.value.trim(),
      Number(subjectIdInput.value),
      gradeInput.value ? Number(gradeInput.value) : undefined,
    )
    await reload()
  } catch (err) {
    saveError.value = (err as { message?: string }).message ?? 'Could not save changes.'
  } finally {
    saving.value = false
  }
}

async function handleSetHomeroom() {
  if (!homeroomTeacherIdInput.value) return
  homeroomError.value = null
  settingHomeroom.value = true
  try {
    await schoolAdminApi.setHomeroomTeacher(classId.value, homeroomTeacherIdInput.value)
    await reload()
  } catch (err) {
    homeroomError.value = (err as { message?: string }).message ?? 'Could not set homeroom teacher.'
  } finally {
    settingHomeroom.value = false
  }
}

onMounted(loadAll)
</script>

<template>
  <div class="class-detail">
    <div class="header">
      <button class="back-link" @click="router.push('/app/school-admin')">← Classes</button>
      <h1 class="page-title">{{ detail?.name ?? 'Class' }}</h1>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <p v-else-if="loadError" class="error-note">{{ loadError }}</p>
    <div v-else-if="detail" class="detail-body">
      <div class="card">
        <h2 class="section-title">Details</h2>
        <form class="details-row" @submit.prevent="handleSave">
          <Field v-model="nameInput" label="Class name" maxlength="50" />
          <SelectField
            v-model="gradeInput"
            label="Grade"
            placeholder="Select grade"
            :options="gradeOptions"
          />
          <SelectField
            v-model="subjectIdInput"
            label="Subject"
            placeholder="Select subject"
            :options="subjectOptions"
          />
          <button type="submit" class="save-btn" :disabled="saving || !nameInput.trim() || !subjectIdInput">
            {{ saving ? 'Saving…' : 'Save' }}
          </button>
        </form>
        <p v-if="saveError" class="error-note">{{ saveError }}</p>
      </div>

      <div class="card">
        <h2 class="section-title">Homeroom teacher</h2>
        <p class="current-homeroom">Current: {{ detail.homeroomTeacherUsername ?? '—' }}</p>
        <form class="details-row" @submit.prevent="handleSetHomeroom">
          <SelectField
            v-model="homeroomTeacherIdInput"
            label="Homeroom teacher"
            placeholder="Select teacher"
            :options="teacherOptions"
            searchable
          />
          <button type="submit" class="save-btn" :disabled="settingHomeroom || !homeroomTeacherIdInput">
            {{ settingHomeroom ? 'Saving…' : 'Set homeroom' }}
          </button>
        </form>
        <p v-if="homeroomError" class="error-note">{{ homeroomError }}</p>
      </div>

      <div class="card">
        <ClassRosterEditor :class-id="classId" :students="detail.students" @changed="reload" />
      </div>

      <div class="card">
        <ClassTeacherAssignmentsEditor
          :class-id="classId"
          :assignments="detail.teacherAssignments"
          :teacher-options="teacherOptions"
          :subject-options="subjectOptions"
          @changed="reload"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.class-detail {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.header {
  display: flex;
  flex-direction: column;
  gap: 4px;
  position: sticky;
  top: -40px;
  z-index: 2;
  padding: 4px 0 12px;
  background: var(--paper);
}

.back-link {
  align-self: flex-start;
  background: none;
  border: none;
  color: var(--green-deep);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
}

.loading {
  color: var(--muted);
  font-size: 14px;
}

.detail-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.section-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 18px;
  color: var(--ink);
}

.current-homeroom {
  margin: 0;
  font-size: 13px;
  color: var(--muted);
}

.details-row {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  flex-wrap: wrap;
}

.details-row > * {
  min-width: 180px;
}

.save-btn {
  background: var(--green-br);
  color: var(--white);
  border: none;
  border-radius: var(--r-sm);
  padding: 8px 18px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  height: 42px;
}

.save-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.error-note {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
