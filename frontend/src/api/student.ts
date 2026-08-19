import { apiFetch } from './client'
import type {
  GenerateExamResponse,
  HistoryMessage,
  SavedExamDetail,
  SavedExamSummary,
  StudentClassesInfo,
  WeakSpot,
} from './types'

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

export function getPracticeQuestions(originalQuestion: string, aiResponse: string): Promise<string[]> {
  return apiFetch<{ questions: string[] }>('/api/student/practice-questions', {
    method: 'POST',
    body: JSON.stringify({ originalQuestion, aiResponse }),
  }).then((res) => res.questions)
}

export function generateExam(topic: string): Promise<GenerateExamResponse> {
  return apiFetch<GenerateExamResponse>('/api/student/exam', {
    method: 'POST',
    body: JSON.stringify({ topic }),
  })
}

export function getExams(): Promise<SavedExamSummary[]> {
  return apiFetch<SavedExamSummary[]>('/api/student/exams')
}

export function getExamDetail(id: number): Promise<SavedExamDetail> {
  return apiFetch<SavedExamDetail>(`/api/student/exams/${id}`)
}
