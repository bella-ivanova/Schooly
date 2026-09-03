<script setup lang="ts">
import ModalShell from './ModalShell.vue'

withDefaults(
  defineProps<{
    title: string
    message: string
    confirmLabel?: string
    loading?: boolean
  }>(),
  { confirmLabel: 'Delete', loading: false },
)

const emit = defineEmits<{ close: []; confirm: [] }>()
</script>

<template>
  <ModalShell :title="title" @close="emit('close')">
    <p class="message">{{ message }}</p>
    <div class="actions">
      <button type="button" class="cancel-btn" :disabled="loading" @click="emit('close')">Cancel</button>
      <button type="button" class="confirm-btn" :disabled="loading" @click="emit('confirm')">
        {{ loading ? `${confirmLabel}…` : confirmLabel }}
      </button>
    </div>
  </ModalShell>
</template>

<style scoped>
.message {
  margin: 0;
  font-size: 14px;
  color: var(--ink-2);
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
  background: var(--t-lit);
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
</style>
