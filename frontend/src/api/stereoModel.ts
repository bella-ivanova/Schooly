import { apiFetch } from './client'
import type { GenerateStereoModelResponse, SavedStereoModelDetail, SavedStereoModelSummary } from './types'

export function generateStereoModel(question: string): Promise<GenerateStereoModelResponse> {
  return apiFetch<GenerateStereoModelResponse>('/api/stereo-models', {
    method: 'POST',
    body: JSON.stringify({ question }),
  })
}

export function getStereoModels(): Promise<SavedStereoModelSummary[]> {
  return apiFetch<SavedStereoModelSummary[]>('/api/stereo-models')
}

export function getStereoModelDetail(id: number): Promise<SavedStereoModelDetail> {
  return apiFetch<SavedStereoModelDetail>(`/api/stereo-models/${id}`)
}
