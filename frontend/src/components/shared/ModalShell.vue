<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'

defineProps<{
  title: string
}>()

const emit = defineEmits<{ close: [] }>()

// Parents unmount the modal via v-if the moment `close` fires, so the exit
// animation has to run here first and only then hand `close` to the parent.
const closing = ref(false)
const EXIT_MS = 180

const reduceMotion =
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

function requestClose() {
  if (closing.value) return
  if (reduceMotion) {
    emit('close')
    return
  }
  closing.value = true
  window.setTimeout(() => emit('close'), EXIT_MS)
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') requestClose()
}

let previousOverflow = ''

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
  previousOverflow = document.documentElement.style.overflow
  document.documentElement.style.overflow = 'hidden'
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
  document.documentElement.style.overflow = previousOverflow
})
</script>

<template>
  <Teleport to="body">
    <div class="modal-overlay" :class="{ closing }" @click.self="requestClose">
      <div class="modal-card" role="dialog" aria-modal="true" :aria-label="title">
        <div class="sheet-grabber" aria-hidden="true"></div>
        <div class="modal-header">
          <h2 class="modal-title">{{ title }}</h2>
          <button type="button" class="close-btn" aria-label="Close" @click="requestClose">×</button>
        </div>
        <div class="modal-body">
          <slot />
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(43, 45, 43, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  z-index: var(--z-modal);
  animation: overlay-in var(--dur-modal) var(--ease-out);
}

.modal-card {
  background: var(--card);
  border-radius: var(--r-lg);
  box-shadow: var(--shadow-lg);
  padding: 24px;
  width: 100%;
  max-width: 480px;
  max-height: calc(100dvh - 40px);
  overflow-y: auto;
  overscroll-behavior: contain;
  display: flex;
  flex-direction: column;
  gap: 16px;
  animation: card-in var(--dur-modal) var(--ease-out);
}

.modal-overlay.closing {
  opacity: 0;
  transition: opacity 180ms ease-out;
}

.modal-overlay.closing .modal-card {
  transform: scale(0.97);
  transition: transform 180ms ease-out;
}

@keyframes overlay-in {
  from {
    opacity: 0;
  }
}

@keyframes card-in {
  from {
    opacity: 0;
    transform: scale(0.96);
  }
}

.sheet-grabber {
  display: none;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.modal-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 22px;
  color: var(--ink);
}

.close-btn {
  flex-shrink: 0;
  display: grid;
  place-items: center;
  width: 36px;
  height: 36px;
  margin: -6px -8px -6px 0;
  background: none;
  border: none;
  border-radius: 50%;
  font-size: 22px;
  line-height: 1;
  color: var(--muted);
  cursor: pointer;
}

@media (hover: hover) and (pointer: fine) {
  .close-btn:hover {
    background: var(--cream-2);
    color: var(--ink);
  }
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

@media (pointer: coarse) {
  .close-btn {
    width: var(--tap);
    height: var(--tap);
  }
}

/* Phones: a bottom sheet — thumb-reachable, slides from the edge it lives on */
@media (max-width: 860px) {
  .modal-overlay {
    align-items: flex-end;
    padding: 0;
  }

  .modal-card {
    max-width: none;
    max-height: 90dvh;
    border-radius: var(--r-xl) var(--r-xl) 0 0;
    padding: 12px 20px calc(24px + env(safe-area-inset-bottom, 0px));
    animation: sheet-in 360ms var(--ease-drawer);
  }

  .modal-overlay.closing .modal-card {
    transform: translateY(100%);
    transition: transform 200ms var(--ease-drawer);
  }

  .sheet-grabber {
    display: block;
    flex-shrink: 0;
    align-self: center;
    width: 40px;
    height: 5px;
    border-radius: 3px;
    background: var(--line-2);
  }

  .modal-title {
    font-size: 20px;
  }
}

@keyframes sheet-in {
  from {
    transform: translateY(100%);
  }
}

@media (prefers-reduced-motion: reduce) {
  .modal-card {
    animation: overlay-in var(--dur-modal) ease;
  }
}
</style>
