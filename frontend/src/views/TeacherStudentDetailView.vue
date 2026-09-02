<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import TeacherStudentDetail from '../components/dashboard/teacher/TeacherStudentDetail.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <TeacherStudentDetail />
  </AppShell>
</template>
