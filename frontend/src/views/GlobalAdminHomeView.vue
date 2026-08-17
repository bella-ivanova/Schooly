<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import GlobalAdminHome from '../components/dashboard/globaladmin/GlobalAdminHome.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Schools', active: true },
  { label: 'All Classes', disabled: true },
  { label: 'All Users', disabled: true },
  { label: 'Subjects', disabled: true },
  { label: 'Curriculum Files', disabled: true },
]
</script>

<template>
  <AppShell role-label="Global Admin" :nav-items="navItems">
    <GlobalAdminHome />
  </AppShell>
</template>
