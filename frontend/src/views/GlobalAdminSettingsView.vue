<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SettingsPanel from '../components/settings/SettingsPanel.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Schools', to: '/app/global-admin' },
  { label: 'All Classes', disabled: true },
  { label: 'All Users', disabled: true },
  { label: 'Subjects', disabled: true },
  { label: 'Curriculum Files', disabled: true },
  { label: 'Settings', active: true },
]
</script>

<template>
  <AppShell role-label="Global Admin" :nav-items="navItems">
    <SettingsPanel />
  </AppShell>
</template>
