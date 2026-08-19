<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import StudentHome from '../components/dashboard/student/StudentHome.vue'
import { useStudentNav } from '../composables/useStudentNav'

const { navItems } = useStudentNav()
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <StudentHome />
  </AppShell>
</template>
