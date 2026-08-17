import { apiFetch } from './client'
import type { AdminClassSummary, AdminSubjectSummary, AdminUserSummary, SchoolInfo, SchoolTeacherCode } from './types'

export function getSchool(): Promise<SchoolInfo> {
  return apiFetch<SchoolInfo>('/api/admin/school')
}

export function getClasses(): Promise<AdminClassSummary[]> {
  return apiFetch<AdminClassSummary[]>('/api/admin/classes')
}

export function getUsers(): Promise<AdminUserSummary[]> {
  return apiFetch<AdminUserSummary[]>('/api/admin/users')
}

export function getSubjects(): Promise<AdminSubjectSummary[]> {
  return apiFetch<AdminSubjectSummary[]>('/api/admin/subjects')
}

export function createSubject(name: string): Promise<void> {
  return apiFetch<void>('/api/admin/subjects', {
    method: 'POST',
    body: JSON.stringify({ name }),
  })
}

export function createClass(name: string, subjectId: number, homeroomTeacherId?: string): Promise<void> {
  return apiFetch<void>('/api/admin/classes', {
    method: 'POST',
    body: JSON.stringify({ name, subjectId, homeroomTeacherId }),
  })
}

export function getTeacherCode(): Promise<SchoolTeacherCode | null> {
  return apiFetch<SchoolTeacherCode | null>('/api/admin/teacher-code')
}

export function regenerateTeacherCode(): Promise<SchoolTeacherCode> {
  return apiFetch<SchoolTeacherCode>('/api/admin/teacher-code/regenerate', { method: 'POST' })
}
