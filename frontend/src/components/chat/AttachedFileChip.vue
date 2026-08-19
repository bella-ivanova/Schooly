<script setup lang="ts">
defineProps<{
  filename: string
  chunks: number
}>()
</script>

<template>
  <div class="attached-chip">
    <span class="chip-icon">📄</span>
    <span class="chip-name">{{ filename }}</span>
    <span class="chip-status">Uploaded ✓</span>
    <span class="chip-scope">· scoped to this chat</span>
  </div>
</template>

<style scoped>
.attached-chip {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: var(--r-sm);
  background: var(--cream-2);
  font-size: 13px;
  color: var(--ink-2);
  width: fit-content;
}

.chip-name {
  font-weight: 600;
}

.chip-status {
  color: var(--green-deep);
  font-weight: 600;
}

.chip-scope {
  color: var(--muted);
}
</style>
