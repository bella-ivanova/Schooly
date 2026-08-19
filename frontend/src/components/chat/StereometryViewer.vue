<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as chatApi from '../../api/chat'

const props = defineProps<{
  scene: string
}>()

const html = ref<string | null>(null)
const loading = ref(true)
const error = ref(false)

onMounted(async () => {
  try {
    html.value = await chatApi.getSceneHtml(props.scene)
  } catch {
    error.value = true
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="stereo-viewer">
    <div v-if="loading" class="stereo-state">Loading 3D viewer…</div>
    <div v-else-if="error" class="stereo-state">Could not load the 3D viewer.</div>
    <iframe v-else :srcdoc="html ?? ''" sandbox="allow-scripts" class="stereo-frame" title="3D stereometry viewer" />
  </div>
</template>

<style scoped>
.stereo-viewer {
  margin-top: 12px;
  border-radius: var(--r-sm);
  overflow: hidden;
  border: 1px solid var(--line);
}

.stereo-state {
  padding: 24px;
  text-align: center;
  font-size: 13px;
  color: var(--muted);
  background: var(--cream-2);
}

.stereo-frame {
  display: block;
  width: 100%;
  height: 360px;
  border: none;
}
</style>
