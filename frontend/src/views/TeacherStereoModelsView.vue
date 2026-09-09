<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import StereoModelPanel from '../components/stereoModel/StereoModelPanel.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <StereoModelPanel base-path="/app/teacher/stereo-models" />
  </AppShell>
</template>
