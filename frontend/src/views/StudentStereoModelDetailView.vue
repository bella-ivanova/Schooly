<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import StereoModelDetail from '../components/stereoModel/StereoModelDetail.vue'
import { useStudentNav } from '../composables/useStudentNav'

const { navItems } = useStudentNav()
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <StereoModelDetail base-path="/app/student/stereo-models" />
  </AppShell>
</template>
