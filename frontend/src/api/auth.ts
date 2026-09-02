import { apiFetch } from './client'
import type {
  AuthResponse,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RefreshRequest,
  RefreshResponse,
  RegisterRequest,
  ResetPasswordRequest,
  UpdateProfileRequest,
  UserSummary,
  VerifyResetCodeRequest,
} from './types'

export function login(req: LoginRequest): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function register(req: RegisterRequest): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function refresh(req: RefreshRequest): Promise<RefreshResponse> {
  return apiFetch<RefreshResponse>('/api/auth/refresh', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function logout(req: RefreshRequest): Promise<{ message: string }> {
  return apiFetch<{ message: string }>('/api/auth/logout', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function forgotPassword(req: ForgotPasswordRequest): Promise<{ message: string }> {
  return apiFetch<{ message: string }>('/api/auth/forgot-password', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function verifyResetCode(req: VerifyResetCodeRequest): Promise<{ message: string }> {
  return apiFetch<{ message: string }>('/api/auth/verify-reset-code', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function resetPassword(req: ResetPasswordRequest): Promise<{ message: string }> {
  return apiFetch<{ message: string }>('/api/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function updateProfile(req: UpdateProfileRequest): Promise<UserSummary> {
  return apiFetch<UserSummary>('/api/auth/profile', {
    method: 'PUT',
    body: JSON.stringify(req),
  })
}

export function changePassword(req: ChangePasswordRequest): Promise<{ message: string }> {
  return apiFetch<{ message: string }>('/api/auth/change-password', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}
