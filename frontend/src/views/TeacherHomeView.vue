<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import TeacherHome from '../components/dashboard/teacher/TeacherHome.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: '+ New Chat', active: true, disabled: true, primary: true },
  { label: 'Past Chats', disabled: true },
  { label: 'Full Classes List', disabled: true },
  { label: 'Full Student List', disabled: true },
  { label: 'Settings', disabled: true },
]
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <TeacherHome />
  </AppShell>
</template>
