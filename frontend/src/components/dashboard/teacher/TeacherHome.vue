<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import * as teacherApi from '../../../api/teacher'
import type { TeacherActivityEntry, TeacherClassSummary, TeacherStruggleGroup } from '../../../api/types'
import ClassCard from './ClassCard.vue'
import StruggleTopicsCard from './StruggleTopicsCard.vue'
import MostActiveStudentsCard from './MostActiveStudentsCard.vue'

const classes = ref<TeacherClassSummary[]>([])
const activity = ref<TeacherActivityEntry[]>([])
const struggles = ref<TeacherStruggleGroup[]>([])
const selectedClassId = ref<number | null>(null)
const loading = ref(true)

const selectedClass = computed(() => classes.value.find((c) => c.class.id === selectedClassId.value))

const selectedClassActivity = computed(
  () => activity.value.find((a) => a.class.id === selectedClassId.value)?.topStudents ?? [],
)

async function loadStruggles(classId: number) {
  struggles.value = await teacherApi.getStruggles(classId)
}

function selectClass(classId: number) {
  selectedClassId.value = classId
}

watch(selectedClassId, (id) => {
  if (id !== null) loadStruggles(id)
})

onMounted(async () => {
  const [classesRes, activityRes] = await Promise.all([teacherApi.getClasses(), teacherApi.getActivity()])
  classes.value = classesRes
  activity.value = activityRes
  if (classesRes.length > 0) selectedClassId.value = classesRes[0].class.id
  loading.value = false
})
</script>

<template>
  <div class="teacher-home">
    <h1 class="page-title">My Classes</h1>

    <div v-if="loading" class="loading">Loading...</div>
    <template v-else>
      <div class="class-grid">
        <ClassCard
          v-for="entry in classes"
          :key="entry.class.id"
          :entry="entry"
          :active="entry.class.id === selectedClassId"
          @select="selectClass(entry.class.id)"
        />
      </div>

      <div v-if="selectedClass" class="detail-grid">
        <StruggleTopicsCard :class-name="selectedClass.class.name" :groups="struggles" />
        <MostActiveStudentsCard :students="selectedClassActivity" />
      </div>
    </template>
  </div>
</template>

<style scoped>
.teacher-home {
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-width: 960px;
}

.page-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 28px;
  color: var(--ink);
}

.loading {
  color: var(--muted);
  font-size: 14px;
}

.class-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 16px;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
}
</style>
