<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import SchoolyMark from '../shared/SchoolyMark.vue'
import type { NavItem } from './navItem'

defineProps<{
  roleLabel: string
  navItems: NavItem[]
}>()

const authStore = useAuthStore()
const router = useRouter()
const route = useRoute()

function isActive(item: NavItem): boolean {
  if (item.active !== undefined) return item.active
  return typeof item.to === 'string' && route.path === item.to
}

async function handleLogout() {
  await authStore.logout()
  await router.push('/login')
}

function goHome() {
  router.push('/app')
}
</script>

<template>
  <aside class="sidebar">
    <div
      class="sidebar-header"
      role="button"
      tabindex="0"
      @click="goHome"
      @keydown.enter="goHome"
    >
      <SchoolyMark :size="28" variant="solid" />
      <span class="wordmark">Schooly</span>
    </div>

    <div class="sidebar-scroll">
      <p class="role-label">{{ roleLabel }}</p>

      <nav class="nav-list">
        <template v-for="item in navItems" :key="item.label">
          <p v-if="item.sectionHeader" class="section-header">{{ item.label }}</p>
          <router-link
            v-else-if="item.to && !item.disabled"
            :to="item.to"
            class="nav-item"
            :class="{ active: isActive(item), primary: item.primary }"
          >
            {{ item.label }}
          </router-link>
          <span
            v-else
            class="nav-item"
            :class="{ active: isActive(item), disabled: item.disabled, primary: item.primary }"
          >
            {{ item.label }}
          </span>
        </template>
      </nav>
    </div>

    <div class="sidebar-footer">
      <span class="user-name">{{ authStore.user?.fullName }}</span>
      <button type="button" class="logout-link" @click="handleLogout">Log out</button>
    </div>
  </aside>
</template>

<style scoped>
.sidebar {
  flex: 0 0 220px;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 24px 16px;
  background: var(--green);
  color: var(--white);
}

.sidebar-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 8px 8px;
  cursor: pointer;
  flex-shrink: 0;
}

.sidebar-scroll {
  flex: 1 1 auto;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  scrollbar-width: thin;
  scrollbar-color: rgba(255, 255, 255, 0.35) transparent;
}

.sidebar-scroll::-webkit-scrollbar {
  width: 6px;
}

.sidebar-scroll::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.35);
  border-radius: var(--r-sm);
}

.sidebar-scroll::-webkit-scrollbar-track {
  background: transparent;
}

.wordmark {
  font-family: var(--font-heading);
  font-size: 18px;
  font-weight: 600;
}

.role-label {
  margin: 8px 0 4px;
  padding: 0 8px;
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.65);
}

.nav-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.section-header {
  margin: 12px 0 2px;
  padding: 0 12px;
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.5);
}

.nav-item {
  display: block;
  padding: 10px 12px;
  border-radius: var(--r-sm);
  font-size: 14px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.9);
  text-decoration: none;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  cursor: default;
}

a.nav-item {
  cursor: pointer;
}

a.nav-item:hover:not(.active) {
  background: rgba(255, 255, 255, 0.08);
}

.nav-item.primary {
  font-weight: 600;
}

.nav-item.active {
  background: var(--cream);
  color: var(--ink);
}

.nav-item.disabled {
  color: rgba(255, 255, 255, 0.45);
}

.nav-item.disabled.active {
  background: rgba(255, 255, 255, 0.18);
  color: rgba(255, 255, 255, 0.75);
}

.sidebar-footer {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px 8px 0;
  border-top: 1px solid rgba(255, 255, 255, 0.15);
}

.user-name {
  font-size: 13px;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.9);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.logout-link {
  align-self: flex-start;
  border: none;
  background: none;
  padding: 0;
  font-family: var(--font-body);
  font-size: 12px;
  color: rgba(255, 255, 255, 0.65);
  cursor: pointer;
}

.logout-link:hover {
  color: var(--white);
  text-decoration: underline;
}
</style>
