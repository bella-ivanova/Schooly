<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import StereoModelDetail from '../components/stereoModel/StereoModelDetail.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <StereoModelDetail base-path="/app/teacher/stereo-models" />
  </AppShell>
</template>
