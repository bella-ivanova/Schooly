<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

const props = defineProps<{
  label: string
  modelValue: string
  options: { value: string; label: string }[]
  placeholder?: string
  searchable?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const query = ref('')
const open = ref(false)
const inputEl = ref<HTMLInputElement | null>(null)
const dropdownStyle = ref<{ top: string; left: string; width: string }>({ top: '0', left: '0', width: '0' })

watch(
  () => [props.modelValue, props.options] as const,
  ([value, options]) => {
    const match = options.find((opt) => opt.value === value)
    query.value = match ? match.label : ''
  },
  { immediate: true },
)

const filteredOptions = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return props.options
  return props.options.filter((opt) => opt.label.toLowerCase().includes(q))
})

function updatePosition() {
  if (!inputEl.value) return
  const rect = inputEl.value.getBoundingClientRect()
  dropdownStyle.value = {
    top: `${rect.bottom + 4}px`,
    left: `${rect.left}px`,
    width: `${rect.width}px`,
  }
}

// A dropdown floated via a fixed position needs to close on scroll rather than
// track it, since it's teleported out of any scrollable ancestor (e.g. a modal)
// that would otherwise clip it.
function closeOnScroll() {
  open.value = false
}

watch(open, (isOpen) => {
  if (isOpen) {
    updatePosition()
    window.addEventListener('scroll', closeOnScroll, true)
    window.addEventListener('resize', closeOnScroll)
  } else {
    window.removeEventListener('scroll', closeOnScroll, true)
    window.removeEventListener('resize', closeOnScroll)
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('scroll', closeOnScroll, true)
  window.removeEventListener('resize', closeOnScroll)
})

function handleInput(event: Event) {
  query.value = (event.target as HTMLInputElement).value
  open.value = true
}

function handleBlur() {
  open.value = false
  const match = props.options.find((opt) => opt.value === props.modelValue)
  query.value = match ? match.label : ''
}

function selectOption(opt: { value: string; label: string }) {
  emit('update:modelValue', opt.value)
  query.value = opt.label
  open.value = false
}
</script>

<template>
  <label class="field">
    <span class="field-label">{{ label }}</span>
    <span class="field-input-wrap">
      <input
        v-if="searchable"
        ref="inputEl"
        class="field-input"
        type="text"
        autocomplete="off"
        :placeholder="placeholder"
        :value="query"
        @input="handleInput"
        @focus="open = true"
        @blur="handleBlur"
      />
      <Teleport v-if="searchable" to="body">
        <ul v-if="open" class="search-options" :style="{ position: 'fixed', ...dropdownStyle }">
          <li v-if="!filteredOptions.length" class="search-empty">No matches</li>
          <li
            v-for="opt in filteredOptions"
            :key="opt.value"
            class="search-option"
            @mousedown.prevent="selectOption(opt)"
          >
            {{ opt.label }}
          </li>
        </ul>
      </Teleport>
      <select
        v-if="!searchable"
        class="field-input"
        :value="modelValue"
        @change="$emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
      >
        <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
        <option v-for="opt in options" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
      </select>
      <span class="caret">▾</span>
    </span>
  </label>
</template>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink-2);
}

.field-input-wrap {
  position: relative;
  display: flex;
}

.field-input {
  flex: 1;
  min-width: 0;
  appearance: none;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  padding: 10px 36px 10px 14px;
  font-size: 15px;
  font-family: inherit;
  color: var(--ink);
  background: var(--white);
  outline: none;
  transition: border-color 0.15s ease;
}

.field-input:focus {
  border-color: var(--green-br);
}

.caret {
  position: absolute;
  right: 14px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--muted);
  pointer-events: none;
  font-size: 12px;
}

.search-options {
  z-index: 1000;
  margin: 0;
  padding: 4px;
  list-style: none;
  background: var(--white);
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  box-shadow: var(--shadow-lg);
  max-height: 220px;
  overflow-y: auto;
}

.search-option {
  padding: 8px 10px;
  border-radius: var(--r-sm);
  font-size: 14px;
  color: var(--ink);
  cursor: pointer;
}

.search-option:hover {
  background: var(--cream-2);
}

.search-empty {
  padding: 8px 10px;
  font-size: 14px;
  color: var(--muted);
}
</style>
