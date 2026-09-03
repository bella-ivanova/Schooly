<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SchoolAdminClassDetail from '../components/dashboard/schooladmin/SchoolAdminClassDetail.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Classes', to: '/app/school-admin' },
  { label: 'Subjects', to: '/app/school-admin/subjects' },
  { label: 'Staff & Students', to: '/app/school-admin/staff' },
  { label: 'Settings', to: '/app/school-admin/settings' },
]
</script>

<template>
  <AppShell role-label="School Admin" :nav-items="navItems">
    <SchoolAdminClassDetail />
  </AppShell>
</template>
