<script setup lang="ts">
import SignInForm from '../components/login/SignInForm.vue'
</script>

<template>
  <main class="login-page">
    <SignInForm />
  </main>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}
</style>
