<script setup lang="ts">
import { computed, ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import Field from '../../shared/Field.vue'
import ConfirmModal from '../../shared/ConfirmModal.vue'
import ReplaceCurriculumFileModal from './ReplaceCurriculumFileModal.vue'

interface FileRow {
  fileKey: string
  subject: string
  fileName: string
}

const gradeInput = ref('10')
const loadedGrade = ref<number | null>(null)
const files = ref<FileRow[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)

const uploadSubject = ref('')
const uploadFileInput = ref<HTMLInputElement | null>(null)
const uploadFile = ref<File | null>(null)
const uploadFileError = ref<string | null>(null)
const uploading = ref(false)
const uploadError = ref<string | null>(null)

const deletingFile = ref<FileRow | null>(null)
const deleting = ref(false)

const replacingFile = ref<FileRow | null>(null)

const MAX_FILE_BYTES = 50 * 1024 * 1024

const gradeValid = computed(() => {
  const n = Number(gradeInput.value)
  return Number.isInteger(n) && n > 0
})

function toFileRow(fileKey: string): FileRow {
  const idx = fileKey.lastIndexOf('/')
  return idx === -1
    ? { fileKey, subject: '(root)', fileName: fileKey }
    : { fileKey, subject: fileKey.slice(0, idx), fileName: fileKey.slice(idx + 1) }
}

async function handleLoadFiles() {
  if (!gradeValid.value) return
  const grade = Number(gradeInput.value)
  loading.value = true
  loadError.value = null
  try {
    const keys = await globalAdminApi.getCurriculumFiles(grade)
    files.value = keys.map(toFileRow)
    loadedGrade.value = grade
  } catch (err) {
    loadError.value = (err as { message?: string }).message ?? 'Could not load files.'
  } finally {
    loading.value = false
  }
}

function handleUploadFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  uploadFileError.value = null
  uploadFile.value = null
  if (!file.name.toLowerCase().endsWith('.pdf')) {
    uploadFileError.value = 'Only PDF files are accepted.'
    return
  }
  if (file.size > MAX_FILE_BYTES) {
    uploadFileError.value = 'File is too large (max 50MB).'
    return
  }
  uploadFile.value = file
}

async function handleUpload() {
  if (!uploadFile.value || loadedGrade.value === null) return
  uploadError.value = null
  uploading.value = true
  try {
    await globalAdminApi.uploadCurriculumFile(loadedGrade.value, uploadSubject.value.trim(), uploadFile.value)
    await handleLoadFiles()
    uploadSubject.value = ''
    uploadFile.value = null
    if (uploadFileInput.value) uploadFileInput.value.value = ''
  } catch (err) {
    uploadError.value = (err as { message?: string }).message ?? 'Could not upload file.'
  } finally {
    uploading.value = false
  }
}

async function handleDeleteFile() {
  if (!deletingFile.value || loadedGrade.value === null) return
  deleting.value = true
  try {
    await globalAdminApi.deleteCurriculumFile(loadedGrade.value, deletingFile.value.fileKey)
    await handleLoadFiles()
    deletingFile.value = null
  } finally {
    deleting.value = false
  }
}

async function handleReplaced() {
  await handleLoadFiles()
}
</script>

<template>
  <div class="global-admin-curriculum">
    <div class="header">
      <h1 class="page-title">Curriculum Files</h1>
      <p class="page-subtitle">Global — grade + subject, no school scoping</p>
    </div>

    <div class="table-card code-card">
      <div class="code-card-header">
        <h2 class="section-title">Grade</h2>
      </div>
      <form class="create-row" @submit.prevent="handleLoadFiles">
        <Field v-model="gradeInput" label="Grade" type="number" placeholder="10" />
        <button type="submit" class="regen-btn create-btn" :disabled="loading || !gradeValid">
          {{ loading ? 'Loading…' : 'Load files' }}
        </button>
      </form>
      <p v-if="loadError" class="create-error">{{ loadError }}</p>
    </div>

    <template v-if="loadedGrade !== null">
      <div class="table-card">
        <table class="files-table">
          <thead>
            <tr>
              <th>Source file</th>
              <th>Subject</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in files" :key="row.fileKey">
              <td class="file-name">{{ row.fileName }}</td>
              <td>{{ row.subject }}</td>
              <td class="actions">
                <button class="link-btn" @click="replacingFile = row">Replace</button>
                <button class="link-btn danger" @click="deletingFile = row">Delete</button>
              </td>
            </tr>
            <tr v-if="files.length === 0">
              <td colspan="3" class="empty">No files ingested for grade {{ loadedGrade }} yet.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-card code-card">
        <div class="code-card-header">
          <h2 class="section-title">Upload a file</h2>
          <p class="section-subtitle">
            Uploads to grade {{ loadedGrade }}. Leave subject blank to upload to the grade's root.
          </p>
        </div>
        <form class="create-row" @submit.prevent="handleUpload">
          <Field v-model="uploadSubject" label="Subject" placeholder="Math" />
          <div class="file-picker">
            <span class="field-label">PDF file</span>
            <input
              ref="uploadFileInput"
              type="file"
              accept="application/pdf,.pdf"
              class="hidden-input"
              @change="handleUploadFileChange"
            />
            <button type="button" class="pick-btn" :disabled="uploading" @click="uploadFileInput?.click()">
              {{ uploadFile ? uploadFile.name : 'Choose PDF' }}
            </button>
          </div>
          <button type="submit" class="regen-btn create-btn" :disabled="uploading || !uploadFile">
            {{ uploading ? 'Uploading & ingesting…' : 'Upload' }}
          </button>
        </form>
        <p v-if="uploadFileError" class="create-error">{{ uploadFileError }}</p>
        <p v-if="uploadError" class="create-error">{{ uploadError }}</p>
      </div>
    </template>

    <ConfirmModal
      v-if="deletingFile"
      title="Delete file"
      :message="`Delete '${deletingFile.fileName}'? This removes its ingested content from the search index (Qdrant) permanently, not just the file.`"
      :loading="deleting"
      @close="deletingFile = null"
      @confirm="handleDeleteFile"
    />

    <ReplaceCurriculumFileModal
      v-if="replacingFile && loadedGrade !== null"
      :grade="loadedGrade"
      :file-key="replacingFile.fileKey"
      @close="replacingFile = null"
      @replaced="handleReplaced"
    />
  </div>
</template>

<style scoped>
.global-admin-curriculum {
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

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
}

.page-subtitle {
  margin: 0;
  font-size: 14px;
  color: var(--muted);
}

.table-card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  overflow: hidden;
}

.files-table {
  width: 100%;
  border-collapse: collapse;
}

.files-table th {
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--muted);
  background: var(--cream-2);
  padding: 14px 20px;
}

.files-table td {
  padding: 14px 20px;
  font-size: 14px;
  color: var(--ink-2);
  border-top: 1px solid var(--line);
}

.file-name {
  font-weight: 600;
  color: var(--ink);
}

.actions {
  display: flex;
  gap: 14px;
  align-items: center;
}

.link-btn {
  background: none;
  border: none;
  color: var(--green-deep);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
  white-space: nowrap;
}

.link-btn.danger {
  color: var(--t-lit);
}

.empty {
  text-align: center;
  color: var(--muted);
}

.code-card {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.code-card-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.section-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 18px;
  color: var(--ink);
}

.section-subtitle {
  margin: 0;
  font-size: 13px;
  color: var(--muted);
}

.create-row {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  flex-wrap: wrap;
}

.create-row > * {
  min-width: 180px;
}

.create-btn {
  height: 42px;
}

.create-error {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}

.file-picker {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink-2);
}

.hidden-input {
  display: none;
}

.pick-btn {
  text-align: left;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 10px 14px;
  font-size: 14px;
  color: var(--ink-2);
  background: var(--white);
  cursor: pointer;
  height: 42px;
}

.pick-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.regen-btn {
  background: var(--green-br);
  color: var(--white);
  border: none;
  border-radius: var(--r);
  padding: 8px 18px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.regen-btn:disabled {
  opacity: 0.6;
  cursor: default;
}
</style>
