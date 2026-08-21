import { computed, onMounted, ref } from 'vue'
import * as chatApi from '../api/chat'
import type { ChatSessionSummary } from '../api/types'
import type { NavItem } from '../components/shell/navItem'

/** Builds the teacher sidebar's nav list, with "Past Chats" populated live from the caller's sessions. */
export function useTeacherNav() {
  const sessions = ref<ChatSessionSummary[]>([])

  async function refreshSessions() {
    sessions.value = await chatApi.getSessions()
  }

  onMounted(refreshSessions)

  const recentSessions = computed(() =>
    [...sessions.value]
      .sort((a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime())
      .slice(0, 3),
  )

  const navItems = computed<NavItem[]>(() => [
    { label: '+ New Chat', to: '/app/teacher/chat', primary: true },
    { label: 'Past Chats', sectionHeader: true },
    ...recentSessions.value.map((s) => ({
      label: s.title ?? 'Untitled chat',
      to: `/app/teacher/chat/${s.id}`,
    })),
    { label: 'All Chats', to: '/app/teacher/chats' },
    { label: 'Classes', to: '/app/teacher/classes' },
    { label: 'Students', disabled: true },
    { label: 'Settings', disabled: true },
  ])

  return { navItems, refreshSessions }
}
