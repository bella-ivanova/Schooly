import { apiFetch } from './client'
import type { AdminClassSummary, SchoolInfo } from './types'

export function getSchool(): Promise<SchoolInfo> {
  return apiFetch<SchoolInfo>('/api/admin/school')
}

export function getClasses(): Promise<AdminClassSummary[]> {
  return apiFetch<AdminClassSummary[]>('/api/admin/classes')
}
