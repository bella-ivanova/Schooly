<script setup lang="ts">
import { ref } from 'vue'
import type { StudentClassEntry } from '../../../api/types'

defineProps<{
  classes: StudentClassEntry[]
  submitting: boolean
  error: string | null
}>()

const emit = defineEmits<{ join: [code: string] }>()

const code = ref('')

function handleSubmit() {
  if (!code.value.trim()) return
  emit('join', code.value.trim())
  code.value = ''
}
</script>

<template>
  <div class="card">
    <h3 class="card-title">My Classes</h3>

    <div v-if="classes.length === 0" class="empty">You haven't joined any classes yet.</div>
    <ul v-else class="class-list">
      <li v-for="cls in classes" :key="cls.classId" class="class-item">{{ cls.className }}</li>
    </ul>

    <form class="join-form" @submit.prevent="handleSubmit">
      <input v-model="code" class="code-input" placeholder="Class code" />
      <button type="submit" class="join-btn" :disabled="submitting || !code.trim()">
        {{ submitting ? 'Joining…' : 'Join' }}
      </button>
    </form>
    <p v-if="error" class="join-error">{{ error }}</p>
  </div>
</template>

<style scoped>
.card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: var(--r);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.card-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 16px;
  color: var(--ink);
}

.empty {
  font-size: 13px;
  color: var(--muted);
}

.class-list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.class-item {
  font-size: 12px;
  font-weight: 600;
  color: var(--green-deep);
  background: var(--sage-soft);
  border-radius: 999px;
  padding: 4px 12px;
}

.join-form {
  display: flex;
  gap: 10px;
}

.code-input {
  flex: 1;
  min-width: 0;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 10px 14px;
  font-size: 15px;
  color: var(--ink);
  background: var(--white);
  outline: none;
}

.code-input:focus {
  border-color: var(--green-br);
}

.join-btn {
  border: none;
  border-radius: var(--r-sm);
  background: var(--green);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 14px;
  font-weight: 600;
  padding: 10px 18px;
  cursor: pointer;
}

.join-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.join-error {
  margin: 0;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
