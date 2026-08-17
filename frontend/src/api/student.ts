import { apiFetch } from './client'
import type { HistoryMessage, StudentClassesInfo, WeakSpot } from './types'

export function getWeakSpots(days = 7): Promise<WeakSpot[]> {
  return apiFetch<WeakSpot[]>(`/api/student/weak-spots?days=${days}`)
}

export function getHistory(limit = 20): Promise<HistoryMessage[]> {
  return apiFetch<HistoryMessage[]>(`/api/student/history?limit=${limit}`)
}

export function getClasses(): Promise<StudentClassesInfo> {
  return apiFetch<StudentClassesInfo>('/api/student/classes')
}

export function joinClass(code: string): Promise<StudentClassesInfo> {
  return apiFetch<StudentClassesInfo>('/api/student/classes', {
    method: 'POST',
    body: JSON.stringify({ code }),
  })
}
