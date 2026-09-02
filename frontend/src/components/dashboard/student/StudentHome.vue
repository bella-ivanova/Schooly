<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as studentApi from '../../../api/student'
import type { ApiError, HistoryMessage, StudentClassEntry, WeakSpot } from '../../../api/types'
import { useAuthStore } from '../../../stores/auth'
import RecentActivityCard from './RecentActivityCard.vue'
import WeakSpotsCard from './WeakSpotsCard.vue'
import JoinClassCard from './JoinClassCard.vue'

const authStore = useAuthStore()

const history = ref<HistoryMessage[]>([])
const weakSpots = ref<WeakSpot[]>([])
const classes = ref<StudentClassEntry[]>([])
const loading = ref(true)

const joiningClass = ref(false)
const joinError = ref<string | null>(null)

const recentActivity = computed(() => {
  const seenTopics = new Set<string>()
  const result: { subject: string; topic: string }[] = []

  for (let i = history.value.length - 1; i >= 0 && result.length < 3; i--) {
    const m = history.value[i]
    if (m.role !== 'user' || !m.topic || seenTopics.has(m.topic)) continue
    seenTopics.add(m.topic)
    result.push({ subject: m.subject, topic: m.topic })
  }

  return result
})

onMounted(async () => {
  const [historyRes, weakSpotsRes, classesRes] = await Promise.all([
    studentApi.getHistory(),
    studentApi.getWeakSpots(),
    studentApi.getClasses(),
  ])
  history.value = historyRes
  weakSpots.value = weakSpotsRes
  classes.value = classesRes.classes
  loading.value = false
})

async function handleJoinClass(code: string) {
  joinError.value = null
  joiningClass.value = true
  try {
    const res = await studentApi.joinClass(code)
    classes.value = res.classes
  } catch (err) {
    const apiError = err as ApiError
    joinError.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not join class.'
  } finally {
    joiningClass.value = false
  }
}
</script>

<template>
  <div class="student-home">
    <div class="welcome">
      <h1 class="page-title">Welcome back, {{ authStore.user?.fullName }}</h1>
      <p class="page-subtitle">Grade {{ authStore.user?.grade }} · Bulgarian curriculum</p>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else class="card-grid">
      <RecentActivityCard :entries="recentActivity" />
      <WeakSpotsCard :weak-spots="weakSpots" />
      <JoinClassCard :classes="classes" :submitting="joiningClass" :error="joinError" @join="handleJoinClass" />
    </div>
  </div>
</template>

<style scoped>
.student-home {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.welcome {
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

.loading {
  color: var(--muted);
  font-size: 14px;
}

.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
}
</style>
