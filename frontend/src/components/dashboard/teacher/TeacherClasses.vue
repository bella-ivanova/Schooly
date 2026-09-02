<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as teacherApi from '../../../api/teacher'
import type { TeacherClassSummary } from '../../../api/types'
import ClassCard from './ClassCard.vue'

const router = useRouter()
const classes = ref<TeacherClassSummary[]>([])
const loading = ref(true)

function openClass(classId: number) {
  router.push(`/app/teacher/classes/${classId}`)
}

onMounted(async () => {
  classes.value = await teacherApi.getClasses()
  loading.value = false
})
</script>

<template>
  <div class="teacher-classes">
    <h1 class="page-title">Classes</h1>

    <div v-if="loading" class="loading">Loading...</div>
    <p v-else-if="classes.length === 0" class="empty">You aren't assigned to any classes yet.</p>
    <div v-else class="class-grid">
      <ClassCard
        v-for="entry in classes"
        :key="entry.class.id"
        :entry="entry"
        :active="false"
        @select="openClass(entry.class.id)"
      />
    </div>
  </div>
</template>

<style scoped>
.teacher-classes {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
  position: sticky;
  top: -40px;
  z-index: 2;
  padding: 4px 0 12px;
  background: var(--paper);
}

.loading,
.empty {
  color: var(--muted);
  font-size: 14px;
}

.class-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 16px;
}
</style>
