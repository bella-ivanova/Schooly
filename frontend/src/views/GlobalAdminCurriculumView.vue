<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import GlobalAdminCurriculum from '../components/dashboard/globaladmin/GlobalAdminCurriculum.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Home', to: '/app/global-admin' },
  { label: 'Schools', to: '/app/global-admin/schools' },
  { label: 'All Classes', to: '/app/global-admin/classes' },
  { label: 'All Users', to: '/app/global-admin/users' },
  { label: 'Subjects', to: '/app/global-admin/subjects' },
  { label: 'Curriculum Files', active: true },
  { label: 'Settings', to: '/app/global-admin/settings' },
]
</script>

<template>
  <AppShell role-label="Global Admin" :nav-items="navItems">
    <GlobalAdminCurriculum />
  </AppShell>
</template>
