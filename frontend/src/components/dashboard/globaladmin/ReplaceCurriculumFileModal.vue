<script setup lang="ts">
import { ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import ModalShell from '../../shared/ModalShell.vue'

const props = defineProps<{
  grade: number
  fileKey: string
}>()

const emit = defineEmits<{ close: []; replaced: [] }>()

const subject = props.fileKey.includes('/') ? props.fileKey.slice(0, props.fileKey.lastIndexOf('/')) : '(root)'
const fileName = props.fileKey.includes('/') ? props.fileKey.slice(props.fileKey.lastIndexOf('/') + 1) : props.fileKey

const fileInput = ref<HTMLInputElement | null>(null)
const selectedFile = ref<File | null>(null)
const fileError = ref<string | null>(null)
const replacing = ref(false)
const error = ref<string | null>(null)

const MAX_FILE_BYTES = 50 * 1024 * 1024

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  fileError.value = null
  selectedFile.value = null
  if (!file.name.toLowerCase().endsWith('.pdf')) {
    fileError.value = 'Only PDF files are accepted.'
    return
  }
  if (file.size > MAX_FILE_BYTES) {
    fileError.value = 'File is too large (max 50MB).'
    return
  }
  selectedFile.value = file
}

async function handleReplace() {
  if (!selectedFile.value) return
  error.value = null
  replacing.value = true
  try {
    await globalAdminApi.replaceCurriculumFile(props.grade, props.fileKey, selectedFile.value)
    emit('replaced')
    emit('close')
  } catch (err) {
    error.value = (err as { message?: string }).message ?? 'Could not replace file.'
  } finally {
    replacing.value = false
  }
}
</script>

<template>
  <ModalShell title="Replace file" @close="!replacing && emit('close')">
    <p class="detail-row"><span class="detail-label">File:</span> {{ fileName }}</p>
    <p class="detail-row"><span class="detail-label">Subject:</span> {{ subject }}</p>

    <div class="file-picker">
      <input ref="fileInput" type="file" accept="application/pdf,.pdf" class="hidden-input" @change="handleFileChange" />
      <button type="button" class="pick-btn" :disabled="replacing" @click="fileInput?.click()">
        {{ selectedFile ? selectedFile.name : 'Choose replacement PDF' }}
      </button>
    </div>
    <p v-if="fileError" class="error-note">{{ fileError }}</p>

    <p class="warning">
      Replacing will re-ingest this file and overwrite its content in the search index. This may take a while and
      cannot be undone.
    </p>

    <div class="actions">
      <button type="button" class="cancel-btn" :disabled="replacing" @click="emit('close')">Cancel</button>
      <button type="button" class="confirm-btn" :disabled="replacing || !selectedFile" @click="handleReplace">
        {{ replacing ? 'Replacing…' : 'Replace' }}
      </button>
    </div>
    <p v-if="error" class="error-note">{{ error }}</p>
  </ModalShell>
</template>

<style scoped>
.detail-row {
  margin: 0;
  font-size: 14px;
  color: var(--ink-2);
}

.detail-label {
  font-weight: 600;
  color: var(--ink);
}

.hidden-input {
  display: none;
}

.pick-btn {
  width: 100%;
  text-align: left;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 10px 14px;
  font-size: 14px;
  color: var(--ink-2);
  background: var(--white);
  cursor: pointer;
}

.pick-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.warning {
  margin: 0;
  font-size: 13px;
  color: var(--t-lit);
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.cancel-btn {
  background: none;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 8px 18px;
  font-size: 14px;
  font-weight: 600;
  color: var(--ink-2);
  cursor: pointer;
}

.confirm-btn {
  background: var(--green-br);
  color: var(--white);
  border: none;
  border-radius: var(--r-sm);
  padding: 8px 18px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.cancel-btn:disabled,
.confirm-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.error-note {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
