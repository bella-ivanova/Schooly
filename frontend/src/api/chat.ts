import { API_BASE_URL, buildHeaders } from './client'
import { parseSseStream } from './sse'
import type { ChatMessageRequest, ChatSseFrame } from './types'

/**
 * Not consumed by any UI yet this step - exists so the chat-UI step can import it directly.
 * POST-based SSE stream (not a GET EventSource), per Controllers/ChatController.cs.
 */
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
      yield { kind: 'done', scene: (data.scene as unknown) ?? null }
    } else if ('title' in data) {
      yield { kind: 'meta', title: data.title as string, subject: data.subject as string }
    } else if ('token' in data) {
      yield { kind: 'token', token: data.token as string }
    }
  }
}
