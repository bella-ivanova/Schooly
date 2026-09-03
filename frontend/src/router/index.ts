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
      path: '/app/student/chats',
      name: 'student-chats',
      component: () => import('../views/StudentChatsView.vue'),
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
      path: '/app/student/settings',
      name: 'student-settings',
      component: () => import('../views/StudentSettingsView.vue'),
      meta: { roles: ['student'] },
    },
    {
      path: '/app/teacher',
      name: 'teacher-home',
      component: () => import('../views/TeacherHomeView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/teacher/chat/:sessionId?',
      name: 'teacher-chat',
      component: () => import('../views/TeacherChatView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/teacher/chats',
      name: 'teacher-chats',
      component: () => import('../views/TeacherChatsView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/teacher/classes',
      name: 'teacher-classes',
      component: () => import('../views/TeacherClassesView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/teacher/classes/:classId',
      name: 'teacher-class-detail',
      component: () => import('../views/TeacherClassDetailView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/teacher/students',
      name: 'teacher-students',
      component: () => import('../views/TeacherStudentsView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/teacher/students/:studentId',
      name: 'teacher-student-detail',
      component: () => import('../views/TeacherStudentDetailView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/teacher/settings',
      name: 'teacher-settings',
      component: () => import('../views/TeacherSettingsView.vue'),
      meta: { roles: ['teacher'] },
    },
    {
      path: '/app/school-admin',
      name: 'school-admin-home',
      component: () => import('../views/SchoolAdminHomeView.vue'),
      meta: { roles: ['schooladmin'] },
    },
    {
      path: '/app/school-admin/settings',
      name: 'school-admin-settings',
      component: () => import('../views/SchoolAdminSettingsView.vue'),
      meta: { roles: ['schooladmin'] },
    },
    {
      path: '/app/school-admin/classes/:classId',
      name: 'school-admin-class-detail',
      component: () => import('../views/SchoolAdminClassDetailView.vue'),
      meta: { roles: ['schooladmin'] },
    },
    {
      path: '/app/school-admin/subjects',
      name: 'school-admin-subjects',
      component: () => import('../views/SchoolAdminSubjectsView.vue'),
      meta: { roles: ['schooladmin'] },
    },
    {
      path: '/app/school-admin/staff',
      name: 'school-admin-staff',
      component: () => import('../views/SchoolAdminStaffView.vue'),
      meta: { roles: ['schooladmin'] },
    },
    {
      path: '/app/global-admin',
      name: 'global-admin-home',
      component: () => import('../views/GlobalAdminHomeView.vue'),
      meta: { roles: ['admin'] },
    },
    {
      path: '/app/global-admin/schools',
      name: 'global-admin-schools',
      component: () => import('../views/GlobalAdminSchoolsView.vue'),
      meta: { roles: ['admin'] },
    },
    {
      path: '/app/global-admin/classes',
      name: 'global-admin-classes',
      component: () => import('../views/GlobalAdminClassesView.vue'),
      meta: { roles: ['admin'] },
    },
    {
      path: '/app/global-admin/users',
      name: 'global-admin-users',
      component: () => import('../views/GlobalAdminUsersView.vue'),
      meta: { roles: ['admin'] },
    },
    {
      path: '/app/global-admin/subjects',
      name: 'global-admin-subjects',
      component: () => import('../views/GlobalAdminSubjectsView.vue'),
      meta: { roles: ['admin'] },
    },
    {
      path: '/app/global-admin/curriculum',
      name: 'global-admin-curriculum',
      component: () => import('../views/GlobalAdminCurriculumView.vue'),
      meta: { roles: ['admin'] },
    },
    {
      path: '/app/global-admin/schools/:schoolId',
      name: 'global-admin-school-detail',
      component: () => import('../views/GlobalAdminSchoolDetailView.vue'),
      meta: { roles: ['admin'] },
    },
    {
      path: '/app/global-admin/settings',
      name: 'global-admin-settings',
      component: () => import('../views/GlobalAdminSettingsView.vue'),
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
