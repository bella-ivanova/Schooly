<script setup lang="ts">
import AllChatsView from '../components/chat/AllChatsView.vue'
import AppShell from '../components/shell/AppShell.vue'
import { useTeacherNav } from '../composables/useTeacherNav'

const { navItems, refreshSessions } = useTeacherNav()
</script>

<template>
  <AppShell role-label="Teacher" :nav-items="navItems">
    <AllChatsView base-path="/app/teacher/chat" @session-updated="refreshSessions" />
  </AppShell>
</template>
