<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SettingsPanel from '../components/settings/SettingsPanel.vue'
import { useStudentNav } from '../composables/useStudentNav'

const { navItems } = useStudentNav()
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <SettingsPanel />
  </AppShell>
</template>
