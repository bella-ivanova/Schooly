<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import TeacherClasses from '../components/dashboard/teacher/TeacherClasses.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <TeacherClasses />
  </AppShell>
</template>
