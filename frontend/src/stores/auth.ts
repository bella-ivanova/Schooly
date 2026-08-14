import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import * as authApi from '../api/auth'
import { tokenStorage } from '../api/tokenStorage'
import type { RegisterRequest, UserSummary } from '../api/types'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserSummary | null>(tokenStorage.getUser())
  const token = ref<string | null>(tokenStorage.getToken())

  const isAuthenticated = computed(() => token.value !== null)
  const role = computed(() => user.value?.role ?? null)

  async function login(usernameOrEmail: string, password: string): Promise<void> {
    const res = await authApi.login({ usernameOrEmail, password })
    tokenStorage.setSession(res.token, res.refreshToken, res.user)
    token.value = res.token
    user.value = res.user
  }

  async function register(req: RegisterRequest): Promise<void> {
    const res = await authApi.register(req)
    tokenStorage.setSession(res.token, res.refreshToken, res.user)
    token.value = res.token
    user.value = res.user
  }

  async function logout(): Promise<void> {
    const refreshToken = tokenStorage.getRefreshToken()
    if (refreshToken) {
      try {
        await authApi.logout({ refreshToken })
      } catch {
        // best-effort - clear local state regardless of server-side revocation result
      }
    }
    tokenStorage.clear()
    token.value = null
    user.value = null
  }

  return { user, token, isAuthenticated, role, login, register, logout }
})
