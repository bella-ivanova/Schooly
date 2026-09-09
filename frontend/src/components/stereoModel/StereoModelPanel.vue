<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as stereoModelApi from '../../api/stereoModel'
import type { ApiError, SavedStereoModelSummary } from '../../api/types'

const props = defineProps<{
  basePath: string
}>()

const router = useRouter()

const question = ref('')
const generating = ref(false)
const generateError = ref<string | null>(null)

const models = ref<SavedStereoModelSummary[]>([])
const loadingModels = ref(true)

onMounted(async () => {
  try {
    models.value = await stereoModelApi.getStereoModels()
  } finally {
    loadingModels.value = false
  }
})

async function handleGenerate() {
  if (!question.value.trim() || generating.value) return
  generateError.value = null
  generating.value = true
  try {
    const res = await stereoModelApi.generateStereoModel(question.value.trim())
    router.push(`${props.basePath}/${res.id}`)
  } catch (err) {
    const apiError = err as ApiError
    generateError.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not generate a 3D model.'
  } finally {
    generating.value = false
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="stereo-panel">
    <div class="header">
      <h1 class="page-title">3D Model Generator</h1>
      <p class="page-subtitle">
        Describe a solid shape and its measurements — the AI will build only the 3D model, without solving anything.
      </p>
    </div>

    <form class="generate-form" @submit.prevent="handleGenerate">
      <label class="question-field">
        <span class="question-label">Question</span>
        <textarea
          v-model="question"
          class="question-input"
          rows="3"
          placeholder="e.g. Правилна четириъгълна пирамида с основен ръб 6 см и височина 8 см"
        />
      </label>
      <button type="submit" class="generate-btn" :disabled="generating || !question.trim()">
        {{ generating ? 'Generating…' : 'Generate 3D Model' }}
      </button>
    </form>
    <p v-if="generateError" class="generate-error">{{ generateError }}</p>

    <div class="history">
      <h2 class="history-title">History</h2>
      <div v-if="loadingModels" class="state-msg">Loading…</div>
      <p v-else-if="models.length === 0" class="state-msg">No 3D models generated yet.</p>
      <ul v-else class="model-list">
        <li v-for="model in models" :key="model.id">
          <router-link :to="`${basePath}/${model.id}`" class="model-row">
            <span class="model-question">{{ model.question }}</span>
            <span class="model-date">{{ formatDate(model.createdAt) }}</span>
          </router-link>
        </li>
      </ul>
    </div>
  </div>
</template>

<style scoped>
.stereo-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 1400px;
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

.generate-form {
  display: flex;
  align-items: flex-end;
  gap: 12px;
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 20px;
}

.question-field {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.question-label {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink-2);
}

.question-input {
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 10px 14px;
  font-size: 15px;
  font-family: var(--font-body);
  color: var(--ink);
  background: var(--white);
  outline: none;
  resize: vertical;
  transition: border-color 0.15s ease;
}

.question-input:focus {
  border-color: var(--green-br);
}

.generate-btn {
  border: none;
  border-radius: var(--r-sm);
  background: var(--green-deep);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 14px;
  font-weight: 600;
  padding: 11px 18px;
  cursor: pointer;
  white-space: nowrap;
}

.generate-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.generate-error {
  margin: 0;
  font-size: 13px;
  color: var(--t-lit);
}

.history {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.history-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 16px;
  color: var(--ink);
}

.state-msg {
  font-size: 14px;
  color: var(--muted);
}

.model-list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.model-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
  border-radius: var(--r-sm);
  background: var(--card);
  border: 1px solid var(--line);
  text-decoration: none;
  color: var(--ink);
}

.model-row:hover {
  border-color: var(--green-br);
}

.model-question {
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.model-date {
  font-size: 13px;
  color: var(--muted);
  white-space: nowrap;
}
</style>
