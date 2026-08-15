<script setup lang="ts">
import Sidebar from './Sidebar.vue'
import type { NavItem } from './navItem'

defineProps<{
  roleLabel: string
  navItems: NavItem[]
}>()
</script>

<template>
  <div class="app-shell">
    <Sidebar :role-label="roleLabel" :nav-items="navItems" />
    <main class="app-content">
      <slot />
    </main>
  </div>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
  display: flex;
  background: var(--paper);
}

.app-content {
  flex: 1;
  min-width: 0;
  padding: 40px 48px;
  overflow-y: auto;
}
</style>
