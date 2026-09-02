import { apiFetch } from './client'
import type { ClassJoinCode, TeacherActivityEntry, TeacherClassSummary, TeacherRosterStudent, TeacherStruggleGroup, TeacherStudentStats, TeacherStudentSummary } from './types'

export function getClasses(): Promise<TeacherClassSummary[]> {
  return apiFetch<TeacherClassSummary[]>('/api/teacher/classes')
}

export function getClassRoster(classId: number): Promise<TeacherRosterStudent[]> {
  return apiFetch<TeacherRosterStudent[]>(`/api/teacher/classes/${classId}/students`)
}

export function getAllStudents(): Promise<TeacherStudentSummary[]> {
  return apiFetch<TeacherStudentSummary[]>('/api/teacher/students')
}

export function getStudentDetail(studentId: string): Promise<TeacherStudentSummary> {
  return apiFetch<TeacherStudentSummary>(`/api/teacher/students/${studentId}`)
}

export function getStudentStats(studentId: string, days = 30): Promise<TeacherStudentStats> {
  return apiFetch<TeacherStudentStats>(`/api/teacher/students/${studentId}/stats?days=${days}`)
}

export function getStruggles(classId: number, days = 30): Promise<TeacherStruggleGroup[]> {
  return apiFetch<TeacherStruggleGroup[]>(`/api/teacher/classes/${classId}/struggles?days=${days}`)
}

export function getActivity(days = 30): Promise<TeacherActivityEntry[]> {
  return apiFetch<TeacherActivityEntry[]>(`/api/teacher/activity?days=${days}`)
}

export function getClassJoinCode(classId: number): Promise<ClassJoinCode | null> {
  return apiFetch<ClassJoinCode | null>(`/api/teacher/classes/${classId}/join-code`)
}

export function regenerateClassJoinCode(classId: number): Promise<ClassJoinCode> {
  return apiFetch<ClassJoinCode>(`/api/teacher/classes/${classId}/join-code/regenerate`, { method: 'POST' })
}
