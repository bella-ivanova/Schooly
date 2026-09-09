<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import StereoModelPanel from '../components/stereoModel/StereoModelPanel.vue'
import { useStudentNav } from '../composables/useStudentNav'

const { navItems } = useStudentNav()
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <StereoModelPanel base-path="/app/student/stereo-models" />
  </AppShell>
</template>
