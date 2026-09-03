<script setup lang="ts">
import { ref } from 'vue'
import * as globalAdminApi from '../../../api/globalAdmin'
import ModalShell from '../../shared/ModalShell.vue'
import SelectField from '../../shared/SelectField.vue'

const props = defineProps<{
  userId: string
  userLabel: string
  schoolOptions: { value: string; label: string }[]
}>()

const emit = defineEmits<{ close: []; promoted: [] }>()

const schoolId = ref('')
const saving = ref(false)
const error = ref<string | null>(null)

async function handleSubmit() {
  if (!schoolId.value) return
  error.value = null
  saving.value = true
  try {
    await globalAdminApi.makeSchoolAdmin(props.userId, Number(schoolId.value))
    emit('promoted')
    emit('close')
  } catch (err) {
    error.value = (err as { message?: string }).message ?? 'Could not update role.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <ModalShell title="Make School Admin" @close="emit('close')">
    <p class="intro">Make <strong>{{ userLabel }}</strong> a School Admin for:</p>
    <form class="form-row" @submit.prevent="handleSubmit">
      <SelectField
        v-model="schoolId"
        label="School"
        placeholder="Select school"
        :options="schoolOptions"
        searchable
      />
      <button type="submit" class="save-btn" :disabled="saving || !schoolId">
        {{ saving ? 'Saving…' : 'Make School Admin' }}
      </button>
    </form>
    <p v-if="error" class="error-note">{{ error }}</p>
  </ModalShell>
</template>

<style scoped>
.intro {
  margin: 0;
  font-size: 14px;
  color: var(--ink-2);
}

.form-row {
  display: flex;
  align-items: flex-end;
  gap: 14px;
  flex-wrap: wrap;
}

.form-row > * {
  min-width: 200px;
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
