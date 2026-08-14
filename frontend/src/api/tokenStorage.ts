import type { UserSummary } from './types'

/**
 * Known trade-off: localStorage is XSS-exposed. httpOnly cookies would be more
 * secure but require backend changes (cookie issuance, CSRF handling) that are
 * out of scope for this scaffold.
 */
const TOKEN_KEY = 'sa_token'
const REFRESH_TOKEN_KEY = 'sa_refresh_token'
const USER_KEY = 'sa_user'

export const tokenStorage = {
  getToken: (): string | null => localStorage.getItem(TOKEN_KEY),
  getRefreshToken: (): string | null => localStorage.getItem(REFRESH_TOKEN_KEY),
  getUser: (): UserSummary | null => {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as UserSummary) : null
  },

  setSession: (token: string, refreshToken: string, user?: UserSummary): void => {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
    if (user) localStorage.setItem(USER_KEY, JSON.stringify(user))
  },

  clear: (): void => {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  },
}
