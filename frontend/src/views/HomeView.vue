<script setup lang="ts">
import { computed } from 'vue'
import AppShell from '../components/shell/AppShell.vue'
import type { NavItem } from '../components/shell/navItem'
import StudentHome from '../components/dashboard/student/StudentHome.vue'
import TeacherHome from '../components/dashboard/teacher/TeacherHome.vue'
import SchoolAdminHome from '../components/dashboard/schooladmin/SchoolAdminHome.vue'
import GlobalAdminHome from '../components/dashboard/globaladmin/GlobalAdminHome.vue'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()

const NAV_ITEMS: Record<string, NavItem[]> = {
  student: [
    { label: '+ New Chat', active: true, disabled: true, primary: true },
    { label: 'Past Chats', disabled: true },
    { label: 'Settings', disabled: true },
  ],
  teacher: [
    { label: '+ New Chat', active: true, disabled: true, primary: true },
    { label: 'Past Chats', disabled: true },
    { label: 'Full Classes List', disabled: true },
    { label: 'Full Student List', disabled: true },
    { label: 'Settings', disabled: true },
  ],
  schooladmin: [
    { label: 'Classes', active: true },
    { label: 'Subjects', disabled: true },
    { label: 'Users', disabled: true },
  ],
  admin: [
    { label: 'Schools', active: true },
    { label: 'All Classes', disabled: true },
    { label: 'All Users', disabled: true },
    { label: 'Subjects', disabled: true },
    { label: 'Curriculum Files', disabled: true },
  ],
}

const ROLE_LABELS: Record<string, string> = {
  student: 'Student',
  teacher: 'Teacher',
  schooladmin: 'School Admin',
  admin: 'Global Admin',
}

const role = computed(() => authStore.role ?? 'student')
const navItems = computed(() => NAV_ITEMS[role.value] ?? [])
const roleLabel = computed(() => ROLE_LABELS[role.value] ?? '')
</script>

<template>
  <AppShell :role-label="roleLabel" :nav-items="navItems">
    <StudentHome v-if="role === 'student'" />
    <TeacherHome v-else-if="role === 'teacher'" />
    <SchoolAdminHome v-else-if="role === 'schooladmin'" />
    <GlobalAdminHome v-else-if="role === 'admin'" />
  </AppShell>
</template>
