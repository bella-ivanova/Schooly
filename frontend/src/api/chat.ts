import { API_BASE_URL, apiFetch, buildHeaders, toApiError } from './client'
import { parseSseStream } from './sse'
import type {
  ChatMessageRequest,
  ChatSessionMessage,
  ChatSessionSummary,
  ChatSseFrame,
  ChatUploadResponse,
  SceneHtmlResponse,
} from './types'

/** POST-based SSE stream (not a GET EventSource), per Controllers/ChatController.cs. */
export async function* streamChatMessage(
  message: string,
  sessionId?: number,
): AsyncGenerator<ChatSseFrame> {
  const req: ChatMessageRequest = { message, sessionId }

  const response = await fetch(`${API_BASE_URL}/api/chat/message`, {
    method: 'POST',
    headers: buildHeaders(true),
    body: JSON.stringify(req),
  })

  if (!response.ok || !response.body) {
    throw new Error(`Chat stream request failed with status ${response.status}`)
  }

  for await (const raw of parseSseStream(response.body)) {
    const data = JSON.parse(raw) as Record<string, unknown>

    if ('sessionId' in data) {
      yield { kind: 'session', sessionId: data.sessionId as number }
    } else if ('done' in data) {
      yield { kind: 'done', scene: (data.scene as string | null) ?? null }
    } else if ('title' in data) {
      yield {
        kind: 'meta',
        title: data.title as string,
        subject: data.subject as string,
        classId: (data.classId as number | null) ?? null,
        className: (data.className as string | null) ?? null,
      }
    } else if ('token' in data) {
      yield { kind: 'token', token: data.token as string }
    }
  }
}

export function getSessions(subject?: string): Promise<ChatSessionSummary[]> {
  const query = subject ? `?subject=${encodeURIComponent(subject)}` : ''
  return apiFetch<ChatSessionSummary[]>(`/api/chat/sessions${query}`)
}

export function getSessionMessages(id: number): Promise<ChatSessionMessage[]> {
  return apiFetch<ChatSessionMessage[]>(`/api/chat/sessions/${id}/messages`)
}

export function deleteSession(id: number): Promise<void> {
  return apiFetch<void>(`/api/chat/sessions/${id}`, { method: 'DELETE' })
}

export function getSceneHtml(scene: string): Promise<string> {
  return apiFetch<SceneHtmlResponse>('/api/chat/scene-html', {
    method: 'POST',
    body: JSON.stringify({ scene }),
  }).then((res) => res.html)
}

/** Multipart upload — can't use apiFetch, which forces Content-Type: application/json whenever a body is set. */
export async function uploadChatFile(file: File): Promise<ChatUploadResponse> {
  const formData = new FormData()
  formData.append('file', file)

  const response = await fetch(`${API_BASE_URL}/api/chat/upload`, {
    method: 'POST',
    headers: buildHeaders(false),
    body: formData,
  })

  if (!response.ok) {
    throw await toApiError(response)
  }

  return (await response.json()) as ChatUploadResponse
}
