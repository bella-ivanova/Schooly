import { apiFetch } from './client'
import type {
  AdminClassDetail,
  AdminClassSummary,
  AdminSubjectSummary,
  AdminTeacherSubject,
  AdminUserSummary,
  SchoolInfo,
  SchoolTeacherCode,
} from './types'

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

export function createClass(
  name: string,
  subjectId: number,
  homeroomTeacherId?: string,
  grade?: number,
): Promise<void> {
  return apiFetch<void>('/api/admin/classes', {
    method: 'POST',
    body: JSON.stringify({ name, subjectId, homeroomTeacherId, grade }),
  })
}

export function getTeacherCode(): Promise<SchoolTeacherCode | null> {
  return apiFetch<SchoolTeacherCode | null>('/api/admin/teacher-code')
}

export function regenerateTeacherCode(): Promise<SchoolTeacherCode> {
  return apiFetch<SchoolTeacherCode>('/api/admin/teacher-code/regenerate', { method: 'POST' })
}

export function getClassDetail(classId: number): Promise<AdminClassDetail> {
  return apiFetch<AdminClassDetail>(`/api/admin/classes/${classId}`)
}

export function updateClass(classId: number, name: string, subjectId: number, grade?: number): Promise<void> {
  return apiFetch<void>(`/api/admin/classes/${classId}`, {
    method: 'PUT',
    body: JSON.stringify({ name, subjectId, grade }),
  })
}

export function removeStudent(classId: number, userId: string): Promise<void> {
  return apiFetch<void>(`/api/admin/classes/${classId}/students/${userId}`, { method: 'DELETE' })
}

export function assignTeacherToClass(classId: number, teacherId: string, subjectName: string): Promise<void> {
  return apiFetch<void>(`/api/admin/classes/${classId}/teachers`, {
    method: 'POST',
    body: JSON.stringify({ teacherId, subjectName }),
  })
}

export function removeTeacherFromClass(classId: number, teacherId: string, subjectId: number): Promise<void> {
  return apiFetch<void>(`/api/admin/classes/${classId}/teachers/${teacherId}/subjects/${subjectId}`, {
    method: 'DELETE',
  })
}

export function setHomeroomTeacher(classId: number, teacherId: string): Promise<void> {
  return apiFetch<void>(`/api/admin/classes/${classId}/homeroom`, {
    method: 'PUT',
    body: JSON.stringify({ teacherId }),
  })
}

export function getTeacherSubjects(teacherId: string): Promise<AdminTeacherSubject[]> {
  return apiFetch<AdminTeacherSubject[]>(`/api/admin/teachers/${teacherId}/subjects`)
}

export function assignSubjectToTeacher(teacherId: string, subjectId: number): Promise<void> {
  return apiFetch<void>(`/api/admin/teachers/${teacherId}/subjects/${subjectId}`, { method: 'POST' })
}

export function removeSubjectFromTeacher(teacherId: string, subjectId: number): Promise<void> {
  return apiFetch<void>(`/api/admin/teachers/${teacherId}/subjects/${subjectId}`, { method: 'DELETE' })
}

export function deleteSubject(subjectId: number): Promise<void> {
  return apiFetch<void>(`/api/admin/subjects/${subjectId}`, { method: 'DELETE' })
}
