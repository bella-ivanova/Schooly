import { apiFetch } from './client'
import type { TeacherActivityEntry, TeacherClassSummary, TeacherStruggleGroup } from './types'

export function getClasses(): Promise<TeacherClassSummary[]> {
  return apiFetch<TeacherClassSummary[]>('/api/teacher/classes')
}

export function getStruggles(classId: number, days = 30): Promise<TeacherStruggleGroup[]> {
  return apiFetch<TeacherStruggleGroup[]>(`/api/teacher/classes/${classId}/struggles?days=${days}`)
}

export function getActivity(days = 30): Promise<TeacherActivityEntry[]> {
  return apiFetch<TeacherActivityEntry[]>(`/api/teacher/activity?days=${days}`)
}
