import { apiFetch } from './client'
import type { ClassJoinCode, TeacherActivityEntry, TeacherClassSummary, TeacherStruggleGroup } from './types'

export function getClasses(): Promise<TeacherClassSummary[]> {
  return apiFetch<TeacherClassSummary[]>('/api/teacher/classes')
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
