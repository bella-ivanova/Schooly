<script setup lang="ts">
import AllChatsView from '../components/chat/AllChatsView.vue'
import AppShell from '../components/shell/AppShell.vue'
import { useStudentNav } from '../composables/useStudentNav'

const { navItems, refreshSessions } = useStudentNav()
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <AllChatsView base-path="/app/student/chat" @session-updated="refreshSessions" />
  </AppShell>
</template>
