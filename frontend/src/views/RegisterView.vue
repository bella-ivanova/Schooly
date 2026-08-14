<script setup lang="ts">
import RegisterForm from '../components/register/RegisterForm.vue'
</script>

<template>
  <main class="register-page">
    <RegisterForm />
  </main>
</template>

<style scoped>
.register-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}
</style>
