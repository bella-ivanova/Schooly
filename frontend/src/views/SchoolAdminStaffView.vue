<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SchoolAdminStaff from '../components/dashboard/schooladmin/SchoolAdminStaff.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Classes', to: '/app/school-admin' },
  { label: 'Subjects', to: '/app/school-admin/subjects' },
  { label: 'Staff & Students', active: true },
  { label: 'Settings', to: '/app/school-admin/settings' },
]
</script>

<template>
  <AppShell role-label="School Admin" :nav-items="navItems">
    <SchoolAdminStaff />
  </AppShell>
</template>
