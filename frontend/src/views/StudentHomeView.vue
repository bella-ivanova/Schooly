<script setup lang="ts">
import AppShell from '../components/shell/AppShell.vue'
import StudentHome from '../components/dashboard/student/StudentHome.vue'
import type { NavItem } from '../components/shell/navItem'

const navItems: NavItem[] = [
  { label: '+ New Chat', active: true, disabled: true, primary: true },
  { label: 'Past Chats', disabled: true },
  { label: 'Settings', disabled: true },
]
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <StudentHome />
  </AppShell>
</template>
