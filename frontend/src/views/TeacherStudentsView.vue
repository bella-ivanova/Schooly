<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import TeacherStudents from '../components/dashboard/teacher/TeacherStudents.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <TeacherStudents />
  </AppShell>
</template>
