<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import GlobalAdminUsers from '../components/dashboard/globaladmin/GlobalAdminUsers.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Home', to: '/app/global-admin' },
  { label: 'Schools', to: '/app/global-admin/schools' },
  { label: 'All Classes', to: '/app/global-admin/classes' },
  { label: 'All Users', active: true },
  { label: 'Subjects', to: '/app/global-admin/subjects' },
  { label: 'Curriculum Files', to: '/app/global-admin/curriculum' },
  { label: 'Settings', to: '/app/global-admin/settings' },
]
</script>

<template>
  <AppShell role-label="Global Admin" :nav-items="navItems">
    <GlobalAdminUsers />
  </AppShell>
</template>
