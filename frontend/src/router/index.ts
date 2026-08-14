import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const PUBLIC_ROUTES = ['login', 'register', 'forgot-password']

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/login' },
    { path: '/login', name: 'login', component: () => import('../views/LoginView.vue') },
    { path: '/register', name: 'register', component: () => import('../views/RegisterView.vue') },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('../views/ForgotPasswordView.vue'),
    },
    { path: '/app', name: 'home', component: () => import('../views/PlaceholderHomeView.vue') },
  ],
})

router.beforeEach((to) => {
  const authStore = useAuthStore()
  const isPublic = PUBLIC_ROUTES.includes(to.name as string)

  if (!isPublic && !authStore.isAuthenticated) {
    return { name: 'login' }
  }
  if (isPublic && authStore.isAuthenticated) {
    return { name: 'home' }
  }
})

export default router
