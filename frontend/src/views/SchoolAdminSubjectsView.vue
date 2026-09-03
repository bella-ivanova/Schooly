<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import SchoolAdminSubjects from '../components/dashboard/schooladmin/SchoolAdminSubjects.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: 'Classes', to: '/app/school-admin' },
  { label: 'Subjects', active: true },
  { label: 'Staff & Students', to: '/app/school-admin/staff' },
  { label: 'Settings', to: '/app/school-admin/settings' },
]
</script>

<template>
  <AppShell role-label="School Admin" :nav-items="navItems">
    <SchoolAdminSubjects />
  </AppShell>
</template>
