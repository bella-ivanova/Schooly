<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Sidebar from './Sidebar.vue'
import SchoolyMark from '../shared/SchoolyMark.vue'
import type { NavItem } from './navItem'

defineProps<{
  roleLabel: string
  navItems: NavItem[]
}>()

const route = useRoute()
const drawerOpen = ref(false)

function closeDrawer() {
  drawerOpen.value = false
}

// A tap on a nav link routes away — the drawer should get out of the way
watch(() => route.fullPath, closeDrawer)

watch(drawerOpen, (open) => {
  document.documentElement.style.overflow = open ? 'hidden' : ''
})

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && drawerOpen.value) closeDrawer()
}

onMounted(() => window.addEventListener('keydown', handleKeydown))
onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
  document.documentElement.style.overflow = ''
})
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <button
        type="button"
        class="menu-btn"
        aria-label="Open menu"
        :aria-expanded="drawerOpen"
        @click="drawerOpen = true"
      >
        <span class="menu-icon" aria-hidden="true"></span>
      </button>
      <router-link to="/app" class="topbar-brand">
        <SchoolyMark :size="26" variant="solid" />
        <span class="topbar-wordmark">Schooly</span>
      </router-link>
      <span class="topbar-role">{{ roleLabel }}</span>
    </header>

    <div class="scrim" :class="{ open: drawerOpen }" aria-hidden="true" @click="closeDrawer"></div>

    <Sidebar
      class="shell-sidebar"
      :class="{ open: drawerOpen }"
      :role-label="roleLabel"
      :nav-items="navItems"
      @close="closeDrawer"
    />

    <main class="app-content">
      <slot />
    </main>
  </div>
</template>

<style scoped>
.app-shell {
  height: 100vh;
  height: 100dvh;
  display: flex;
  overflow: hidden;
  /* clip, unlike hidden, can't be scrolled by code either — keeps the sidebar put */
  overflow: clip;
  background: var(--paper);
}

/* only the content column scrolls; the sidebar stays pinned to the viewport */
.shell-sidebar {
  position: sticky;
  top: 0;
  height: 100vh;
  height: 100dvh;
}

.app-content {
  flex: 1;
  min-width: 0;
  padding: var(--gutter) calc(var(--gutter) + 8px);
  overflow-y: auto;
  overscroll-behavior: contain;
  animation: fade-in 180ms var(--ease-out);
}

.topbar,
.scrim {
  display: none;
}

@media (max-width: 860px) {
  .app-shell {
    flex-direction: column;
  }

  .topbar {
    position: relative;
    z-index: var(--z-topbar);
    flex-shrink: 0;
    display: flex;
    align-items: center;
    gap: 8px;
    padding: env(safe-area-inset-top, 0px) 12px 0 max(4px, env(safe-area-inset-left, 0px));
    min-height: calc(56px + env(safe-area-inset-top, 0px));
    background: var(--green);
    color: var(--white);
    box-shadow: var(--shadow-sm);
  }

  .menu-btn {
    width: var(--tap);
    height: var(--tap);
    display: grid;
    place-items: center;
    border: none;
    border-radius: var(--r-sm);
    background: transparent;
    cursor: pointer;
  }

  .menu-btn:active {
    background: rgba(255, 255, 255, 0.14);
  }

  .menu-icon,
  .menu-icon::before,
  .menu-icon::after {
    display: block;
    width: 20px;
    height: 2px;
    border-radius: 2px;
    background: var(--white);
  }

  .menu-icon {
    position: relative;
  }

  .menu-icon::before,
  .menu-icon::after {
    content: '';
    position: absolute;
    left: 0;
  }

  .menu-icon::before {
    top: -6px;
  }

  .menu-icon::after {
    top: 6px;
  }

  .topbar-brand {
    display: flex;
    align-items: center;
    gap: 8px;
    color: var(--white);
    text-decoration: none;
  }

  .topbar-wordmark {
    font-family: var(--font-heading);
    font-size: 18px;
    font-weight: 600;
  }

  .topbar-role {
    margin-left: auto;
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: rgba(255, 255, 255, 0.7);
  }

  .scrim {
    display: block;
    position: fixed;
    inset: 0;
    z-index: var(--z-drawer);
    background: rgba(43, 45, 43, 0.4);
    opacity: 0;
    pointer-events: none;
    transition: opacity 280ms var(--ease-out);
  }

  .scrim.open {
    opacity: 1;
    pointer-events: auto;
  }

  .shell-sidebar {
    position: fixed;
    top: 0;
    bottom: 0;
    left: 0;
    z-index: calc(var(--z-drawer) + 1);
    width: min(300px, 84vw);
    transform: translateX(-100%);
    visibility: hidden;
    /* exit is quicker than enter; visibility waits for the slide to finish */
    transition:
      transform 220ms var(--ease-drawer),
      visibility 0s linear 220ms;
    box-shadow: var(--shadow-lg);
  }

  .shell-sidebar.open {
    transform: translateX(0);
    visibility: visible;
    transition:
      transform 320ms var(--ease-drawer),
      visibility 0s;
  }

  .app-content {
    padding: var(--gutter) max(var(--gutter), env(safe-area-inset-right, 0px))
      calc(var(--gutter) + env(safe-area-inset-bottom, 0px))
      max(var(--gutter), env(safe-area-inset-left, 0px));
  }
}

@media (max-width: 860px) and (prefers-reduced-motion: reduce) {
  .shell-sidebar,
  .shell-sidebar.open {
    transition: visibility 0s;
  }
}
</style>
