import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import type { UserRole } from '../api/types'

declare module 'vue-router' {
  interface RouteMeta {
    roles?: UserRole[]
  }
}

const PUBLIC_ROUTES = ['login', 'register', 'forgot-password']

const ROLE_HOME: Record<UserRole, string> = {
  student: 'student-home',
  teacher: 'teacher-home',
  schooladmin: 'school-admin-home',
  admin: 'global-admin-home',
}

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
    // Pure redirect target — SignInForm/RegisterForm push here by path, and the
    // guard below sends the caller on to their own role-scoped home before this
    // ever renders.
    { path: '/app', name: 'home', component: { render: () => null } },
    {
      path: '/app/student',
      name: 'student-home',
      component: () => import('../views/StudentHomeView.vue'),
      meta: { roles: ['student'] },
    },
    {
      path: '/app/student/chat/:sessionId?',
      name: 'student-chat',
      component: () => import('../views/StudentChatView.vue'),
      meta: { roles: ['student'] },
    },
    {
      path: '/app/student/exams',
      name: 'student-exams',
      component: () => import('../views/StudentExamsView.vue'),
      meta: { roles: ['student'] },
    },
    {
      path: '/app/student/exams/:id',
      name: 'student-exam-detail',
      component: () => import('../views/StudentExamDetailView.vue'),
      meta: { roles: ['student'] },
    },
    {
      path: '/app/teacher',
      name: 'teacher-home',
      component: () => import('../views/TeacherHomeView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/school-admin',
      name: 'school-admin-home',
      component: () => import('../views/SchoolAdminHomeView.vue'),
      meta: { roles: ['schooladmin'] },
    },
    {
      path: '/app/global-admin',
      name: 'global-admin-home',
      component: () => import('../views/GlobalAdminHomeView.vue'),
      meta: { roles: ['admin'] },
    },
    { path: '/:pathMatch(.*)*', name: 'not-found', redirect: '/app' },
  ],
})

router.beforeEach((to) => {
  const authStore = useAuthStore()
  const isPublic = PUBLIC_ROUTES.includes(to.name as string)

  if (!isPublic && !authStore.isAuthenticated) {
    return { name: 'login' }
  }
  if (isPublic && authStore.isAuthenticated) {
    return { name: ROLE_HOME[authStore.role as UserRole] }
  }
  if (to.name === 'home' && authStore.role) {
    return { name: ROLE_HOME[authStore.role] }
  }
  if (to.meta.roles && authStore.role && !to.meta.roles.includes(authStore.role)) {
    return { name: ROLE_HOME[authStore.role] }
  }
})

export default router
