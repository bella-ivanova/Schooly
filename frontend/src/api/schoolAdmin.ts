import { apiFetch } from './client'
import type { AdminClassSummary, SchoolInfo, SchoolTeacherCode } from './types'

export function getSchool(): Promise<SchoolInfo> {
  return apiFetch<SchoolInfo>('/api/admin/school')
}

export function getClasses(): Promise<AdminClassSummary[]> {
  return apiFetch<AdminClassSummary[]>('/api/admin/classes')
}

export function getTeacherCode(): Promise<SchoolTeacherCode | null> {
  return apiFetch<SchoolTeacherCode | null>('/api/admin/teacher-code')
}

export function regenerateTeacherCode(): Promise<SchoolTeacherCode> {
  return apiFetch<SchoolTeacherCode>('/api/admin/teacher-code/regenerate', { method: 'POST' })
}
