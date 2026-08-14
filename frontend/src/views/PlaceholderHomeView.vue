<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
import { useRouter } from 'vue-router'

const authStore = useAuthStore()
const router = useRouter()

async function handleLogout() {
  await authStore.logout()
  await router.push('/login')
}
</script>

<template>
  <main class="home-page">
    <div class="home-card">
      <h1>Здравей, {{ authStore.user?.fullName }}!</h1>
      <p>Ролята ти е: <strong>{{ authStore.role }}</strong></p>
      <button type="button" class="logout-btn" @click="handleLogout">Изход</button>
    </div>
  </main>
</template>

<style scoped>
.home-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}

.home-card {
  background: var(--card);
  border-radius: var(--r-xl);
  box-shadow: var(--shadow);
  padding: 36px;
  text-align: center;
  max-width: 420px;
}

.logout-btn {
  margin-top: 20px;
  border: none;
  border-radius: var(--r-lg);
  background: var(--green);
  color: var(--white);
  font-family: var(--font-heading);
  font-size: 15px;
  padding: 10px 20px;
  cursor: pointer;
}

.logout-btn:hover {
  background: var(--green-br);
}
</style>
