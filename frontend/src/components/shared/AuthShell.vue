<script setup lang="ts">
import SchoolyMark from './SchoolyMark.vue'

defineProps<{
  eyebrow: string
  heading: string
  subtitle?: string
  brandHeading: string
  brandBody: string
  brandCaption: string
}>()
</script>

<template>
  <div class="auth-page">
    <div class="auth-card">
      <aside class="brand-panel">
        <div class="brand-header">
          <SchoolyMark :size="32" variant="solid" />
          <span class="wordmark">Schooly</span>
        </div>

        <div class="brand-hero">
          <SchoolyMark :size="120" variant="ghost" />
        </div>

        <h2 class="brand-heading">{{ brandHeading }}</h2>
        <p class="brand-body">{{ brandBody }}</p>
        <p class="brand-caption">{{ brandCaption }}</p>
      </aside>

      <section class="content-panel">
        <div class="content-inner">
          <div class="mobile-header">
            <SchoolyMark :size="48" variant="solid" />
            <span class="wordmark">Schooly</span>
          </div>

          <p class="eyebrow">{{ eyebrow }}</p>
          <h1 class="heading">{{ heading }}</h1>
          <p v-if="subtitle" class="subtitle">{{ subtitle }}</p>

          <slot />
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: flex;
  background: var(--bg);
}

.auth-card {
  display: flex;
  width: 100%;
  min-height: 100vh;
  background: var(--card);
}

.brand-panel {
  position: relative;
  overflow: hidden;
  flex: 0 0 40%;
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 36px;
  background: linear-gradient(160deg, var(--green), var(--green-br));
  color: var(--white);
}

.brand-panel::before,
.brand-panel::after {
  content: '';
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.08);
  pointer-events: none;
}

.brand-panel::before {
  width: 260px;
  height: 260px;
  right: -100px;
  bottom: -120px;
}

.brand-panel::after {
  width: 140px;
  height: 140px;
  right: 40px;
  top: -60px;
}

.brand-header {
  display: flex;
  align-items: center;
  gap: 10px;
  position: relative;
  z-index: 1;
}

.wordmark {
  font-family: var(--font-heading);
  font-size: 20px;
  font-weight: 600;
}

.brand-hero {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  z-index: 1;
  min-height: 80px;
}

.brand-heading {
  font-size: 26px;
  color: var(--white);
  position: relative;
  z-index: 1;
}

.brand-body {
  margin: 0;
  font-size: 14px;
  line-height: 1.5;
  color: rgba(255, 255, 255, 0.85);
  position: relative;
  z-index: 1;
}

.brand-caption {
  margin: 0;
  font-size: 12px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.65);
  position: relative;
  z-index: 1;
}

.content-panel {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 44px 48px;
  min-width: 0;
}

.content-inner {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
  max-width: 400px;
}

.mobile-header {
  display: none;
  align-items: center;
  gap: 10px;
  align-self: center;
  margin-bottom: 4px;
}

.mobile-header .wordmark {
  color: var(--ink);
}

.eyebrow {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}

.heading {
  margin: -4px 0 0;
  font-size: 28px;
  color: var(--ink);
}

.subtitle {
  margin: -8px 0 0;
  color: var(--muted);
  font-size: 14px;
}

@media (max-width: 860px) {
  .auth-card {
    flex-direction: column;
  }

  .brand-panel {
    display: none;
  }

  .mobile-header {
    display: flex;
  }

  .content-panel {
    padding: 32px 28px;
  }

  .heading {
    text-align: center;
  }

  .eyebrow,
  .subtitle {
    text-align: center;
  }
}
</style>
