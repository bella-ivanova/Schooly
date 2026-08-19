import { tokenStorage } from './tokenStorage'
import type { ApiError, RefreshResponse } from './types'

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string

// Paths that must never trigger the 401->refresh->retry flow, to avoid infinite loops.
const NO_REFRESH_PATHS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/register']

export function buildHeaders(hasBody: boolean): HeadersInit {
  const headers: Record<string, string> = {}
  const token = tokenStorage.getToken()
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (hasBody) headers['Content-Type'] = 'application/json'
  return headers
}

export async function toApiError(response: Response): Promise<ApiError> {
  let body: { error?: string; errors?: string[] } = {}
  try {
    body = await response.json()
  } catch {
    // non-JSON error body (e.g. network-level failure surfaced as a response) - fall through
  }
  const messages = body.errors ?? (body.error ? [body.error] : ['Request failed.'])
  return { status: response.status, message: messages[0], messages }
}

// Deduplicates concurrent refresh attempts: the first 401 triggers the network call,
// every other concurrent 401 awaits the same in-flight promise instead of firing its own.
let refreshPromise: Promise<RefreshResponse> | null = null

async function refreshSession(): Promise<RefreshResponse> {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      const refreshToken = tokenStorage.getRefreshToken()
      if (!refreshToken) throw new Error('No refresh token available.')

      const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })

      if (!response.ok) {
        tokenStorage.clear()
        throw await toApiError(response)
      }

      const data = (await response.json()) as RefreshResponse
      const user = tokenStorage.getUser()
      tokenStorage.setSession(data.token, data.refreshToken, user ?? undefined)
      return data
    })().finally(() => {
      refreshPromise = null
    })
  }
  return refreshPromise
}

export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const hasBody = options.body != null
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: { ...buildHeaders(hasBody), ...options.headers },
  })

  if (response.ok) {
    const text = await response.text()
    return (text ? JSON.parse(text) : undefined) as T
  }

  if (response.status === 401 && !NO_REFRESH_PATHS.includes(path)) {
    try {
      await refreshSession()
    } catch {
      tokenStorage.clear()
      throw await toApiError(response)
    }

    const retryResponse = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers: { ...buildHeaders(hasBody), ...options.headers },
    })

    if (retryResponse.ok) {
      const text = await retryResponse.text()
      return (text ? JSON.parse(text) : undefined) as T
    }

    tokenStorage.clear()
    throw await toApiError(retryResponse)
  }

  throw await toApiError(response)
}
