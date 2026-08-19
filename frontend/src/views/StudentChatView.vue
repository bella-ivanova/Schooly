<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import ChatView from '../components/chat/ChatView.vue'
import AppShell from '../components/shell/AppShell.vue'
import { useStudentNav } from '../composables/useStudentNav'

const route = useRoute()
const { navItems, refreshSessions } = useStudentNav()

const sessionId = computed(() => {
  const raw = route.params.sessionId
  const value = Array.isArray(raw) ? raw[0] : raw
  return value ? Number(value) : undefined
})
</script>

<template>
  <AppShell role-label="Student" :nav-items="navItems">
    <ChatView :session-id="sessionId" @session-updated="refreshSessions" />
  </AppShell>
</template>
