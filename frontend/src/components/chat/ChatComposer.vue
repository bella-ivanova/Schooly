<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{
  disabled: boolean
}>()

const emit = defineEmits<{
  send: [text: string]
  attach: [file: File]
}>()

const text = ref('')
const fileInput = ref<HTMLInputElement | null>(null)
const fileError = ref<string | null>(null)

const MAX_FILE_BYTES = 50 * 1024 * 1024

function handleSend() {
  const trimmed = text.value.trim()
  if (!trimmed || props.disabled) return
  emit('send', trimmed)
  text.value = ''
}

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  fileError.value = null
  if (!file.name.toLowerCase().endsWith('.pdf')) {
    fileError.value = 'Only PDF files are accepted.'
    return
  }
  if (file.size > MAX_FILE_BYTES) {
    fileError.value = 'File is too large (max 50MB).'
    return
  }
  emit('attach', file)
}
</script>

<template>
  <div class="composer-wrap">
    <p v-if="fileError" class="file-error">{{ fileError }}</p>
    <div class="composer">
      <button
        type="button"
        class="attach-btn"
        :disabled="disabled"
        title="Attach a PDF"
        @click="fileInput?.click()"
      >
        📎
      </button>
      <input
        ref="fileInput"
        type="file"
        accept="application/pdf,.pdf"
        class="hidden-input"
        @change="handleFileChange"
      />
      <input
        v-model="text"
        type="text"
        class="composer-input"
        placeholder="Ask a question about your homework…"
        :disabled="disabled"
        @keydown.enter="handleSend"
      />
      <button type="button" class="send-btn" :disabled="disabled || !text.trim()" @click="handleSend">↑</button>
    </div>
    <p v-if="disabled" class="composer-hint">Response streaming — sending is disabled until it finishes.</p>
  </div>
</template>

<style scoped>
.composer-wrap {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.composer {
  display: flex;
  align-items: center;
  gap: 10px;
  background: var(--white);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 8px 10px;
}

.hidden-input {
  display: none;
}

.attach-btn {
  border: none;
  background: var(--cream-2);
  border-radius: var(--r-sm);
  width: 36px;
  height: 36px;
  font-size: 15px;
  cursor: pointer;
  flex: 0 0 auto;
}

.attach-btn:disabled {
  opacity: 0.5;
  cursor: default;
}

.composer-input {
  flex: 1;
  min-width: 0;
  border: none;
  outline: none;
  font-size: 15px;
  font-family: var(--font-body);
  color: var(--ink);
  background: transparent;
}

.composer-input:disabled {
  color: var(--muted);
}

.send-btn {
  flex: 0 0 auto;
  border: none;
  border-radius: var(--r-sm);
  width: 36px;
  height: 36px;
  background: var(--green-deep);
  color: var(--white);
  font-size: 16px;
  cursor: pointer;
}

.send-btn:disabled {
  background: var(--muted-2);
  cursor: default;
}

.composer-hint,
.file-error {
  margin: 0;
  padding: 0 4px;
  font-size: 12px;
  color: var(--muted);
}

.file-error {
  color: var(--t-lit);
}
</style>
