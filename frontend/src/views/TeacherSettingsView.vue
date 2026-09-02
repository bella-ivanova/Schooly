<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SettingsPanel from '../components/settings/SettingsPanel.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <SettingsPanel />
  </AppShell>
</template>
