<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import TeacherClassDetail from '../components/dashboard/teacher/TeacherClassDetail.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <TeacherClassDetail />
  </AppShell>
</template>
