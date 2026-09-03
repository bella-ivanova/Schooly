import { API_BASE_URL, apiFetch, buildHeaders, toApiError } from './client'
import type {
  AdminClassSummary,
  AdminSubjectSummary,
  AdminUserSummary,
  ChatUploadResponse,
  SchoolSummary,
} from './types'

export function getSchools(): Promise<SchoolSummary[]> {
  return apiFetch<SchoolSummary[]>('/api/global-admin/schools')
}

export function createSchool(name: string): Promise<void> {
  return apiFetch<void>('/api/global-admin/schools', {
    method: 'POST',
    body: JSON.stringify({ name }),
  })
}

export function getClasses(): Promise<AdminClassSummary[]> {
  return apiFetch<AdminClassSummary[]>('/api/global-admin/classes')
}

export function createClass(
  schoolId: number,
  name: string,
  subjectId: number,
  homeroomTeacherId?: string,
): Promise<void> {
  return apiFetch<void>('/api/global-admin/classes', {
    method: 'POST',
    body: JSON.stringify({ schoolId, name, subjectId, homeroomTeacherId }),
  })
}

export function deleteClass(classId: number): Promise<void> {
  return apiFetch<void>(`/api/global-admin/classes/${classId}`, { method: 'DELETE' })
}

export function assignStudentToClass(classId: number, userId: string): Promise<void> {
  return apiFetch<void>(`/api/global-admin/classes/${classId}/students`, {
    method: 'POST',
    body: JSON.stringify({ userId }),
  })
}

export function assignTeacherToClass(
  classId: number,
  schoolId: number,
  teacherId: string,
  subjectName: string,
): Promise<void> {
  return apiFetch<void>(`/api/global-admin/classes/${classId}/teachers`, {
    method: 'POST',
    body: JSON.stringify({ schoolId, teacherId, subjectName }),
  })
}

export function getSubjects(): Promise<AdminSubjectSummary[]> {
  return apiFetch<AdminSubjectSummary[]>('/api/global-admin/subjects')
}

export function createSubject(schoolId: number, name: string): Promise<void> {
  return apiFetch<void>('/api/global-admin/subjects', {
    method: 'POST',
    body: JSON.stringify({ schoolId, name }),
  })
}

export function deleteSubject(subjectId: number): Promise<void> {
  return apiFetch<void>(`/api/global-admin/subjects/${subjectId}`, { method: 'DELETE' })
}

export function getUsers(): Promise<AdminUserSummary[]> {
  return apiFetch<AdminUserSummary[]>('/api/global-admin/users')
}

export function makeSchoolAdmin(userId: string, schoolId: number): Promise<void> {
  return apiFetch<void>(`/api/global-admin/users/${userId}/role`, {
    method: 'PUT',
    body: JSON.stringify({ schoolId }),
  })
}

export function getCurriculumFiles(grade: number): Promise<string[]> {
  return apiFetch<string[]>(`/api/global-admin/curriculum/grades/${grade}/files`)
}

/** The `{*fileKey}` route segment on the backend is a multi-segment catch-all — encode per path segment
 * so a literal `/` inside fileKey (e.g. "Math/algebra.pdf") survives as a path separator, not %2F. */
function encodeFileKey(fileKey: string): string {
  return fileKey.split('/').map(encodeURIComponent).join('/')
}

/** Multipart upload — can't use apiFetch, which forces Content-Type: application/json whenever a body is set. */
export async function uploadCurriculumFile(
  grade: number,
  subject: string,
  file: File,
): Promise<ChatUploadResponse> {
  const formData = new FormData()
  if (subject) formData.append('subject', subject)
  formData.append('file', file)

  const response = await fetch(`${API_BASE_URL}/api/global-admin/curriculum/grades/${grade}/files`, {
    method: 'POST',
    headers: buildHeaders(false),
    body: formData,
  })

  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as ChatUploadResponse
}

/** No `subject` field on replace — the backend derives it from fileKey's directory portion. */
export async function replaceCurriculumFile(
  grade: number,
  fileKey: string,
  file: File,
): Promise<ChatUploadResponse> {
  const formData = new FormData()
  formData.append('file', file)

  const response = await fetch(
    `${API_BASE_URL}/api/global-admin/curriculum/grades/${grade}/files/${encodeFileKey(fileKey)}`,
    {
      method: 'PUT',
      headers: buildHeaders(false),
      body: formData,
    },
  )

  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as ChatUploadResponse
}

export function deleteCurriculumFile(grade: number, fileKey: string): Promise<void> {
  return apiFetch<void>(
    `/api/global-admin/curriculum/grades/${grade}/files/${encodeFileKey(fileKey)}`,
    { method: 'DELETE' },
  )
}
