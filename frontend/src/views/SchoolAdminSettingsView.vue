<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SettingsPanel from '../components/settings/SettingsPanel.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Classes', to: '/app/school-admin' },
  { label: 'Subjects', disabled: true },
  { label: 'Users', disabled: true },
  { label: 'Settings', active: true },
]
</script>

<template>
  <AppShell role-label="School Admin" :nav-items="navItems">
    <SettingsPanel />
  </AppShell>
</template>
