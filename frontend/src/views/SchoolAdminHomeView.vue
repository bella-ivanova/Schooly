<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SchoolAdminHome from '../components/dashboard/schooladmin/SchoolAdminHome.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Classes', active: true },
  { label: 'Subjects', disabled: true },
  { label: 'Users', disabled: true },
]
</script>

<template>
  <AppShell role-label="School Admin" :nav-items="navItems">
    <SchoolAdminHome />
  </AppShell>
</template>
