<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import * as stereoModelApi from '../../api/stereoModel'
import type { ApiError, SavedStereoModelDetail } from '../../api/types'
import StereometryViewer from '../chat/StereometryViewer.vue'

const props = defineProps<{
  basePath: string
}>()

const route = useRoute()

const model = ref<SavedStereoModelDetail | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

async function load(id: number) {
  loading.value = true
  error.value = null
  try {
    model.value = await stereoModelApi.getStereoModelDetail(id)
  } catch (err) {
    const apiError = err as ApiError
    error.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load this 3D model.'
  } finally {
    loading.value = false
  }
}

watch(
  () => route.params.id,
  (id) => {
    const value = Array.isArray(id) ? id[0] : id
    if (value) load(Number(value))
  },
  { immediate: true },
)

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="model-detail">
    <router-link :to="basePath" class="back-link">← Back to 3D Model Generator</router-link>

    <div v-if="loading" class="state-msg">Loading…</div>
    <div v-else-if="error" class="state-msg error">{{ error }}</div>
    <template v-else-if="model">
      <div class="header">
        <h1 class="page-title">{{ model.question }}</h1>
        <p class="page-subtitle">Generated {{ formatDate(model.createdAt) }}</p>
      </div>
      <StereometryViewer :scene="model.scene" />
    </template>
  </div>
</template>

<style scoped>
.model-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 1400px;
}

.back-link {
  align-self: flex-start;
  font-size: 13px;
  font-weight: 600;
  color: var(--green-deep);
  text-decoration: none;
}

.back-link:hover {
  text-decoration: underline;
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
  font-size: 26px;
  color: var(--ink);
}

.page-subtitle {
  margin: 0;
  font-size: 13px;
  color: var(--muted);
}

.state-msg {
  font-size: 14px;
  color: var(--muted);
}

.state-msg.error {
  color: var(--t-lit);
}
</style>
