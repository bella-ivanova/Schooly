import { apiFetch } from './client'
import type { SchoolSummary } from './types'

export function getSchools(): Promise<SchoolSummary[]> {
  return apiFetch<SchoolSummary[]>('/api/global-admin/schools')
}
