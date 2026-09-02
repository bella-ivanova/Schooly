<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import * as chatApi from '../../api/chat'
import * as studentApi from '../../api/student'
import type { ApiError } from '../../api/types'
import AttachedFileChip from './AttachedFileChip.vue'
import ChatComposer from './ChatComposer.vue'
import MessageBubble from './MessageBubble.vue'

interface DisplayMessage {
  role: 'user' | 'assistant'
  content: string
  subject?: string | null
  topic?: string | null
  scene?: string | null
  streaming?: boolean
  practiceQuestions?: string[] | null
  loadingPracticeQuestions?: boolean
}

const props = withDefaults(
  defineProps<{
    sessionId?: number
    basePath?: string
    showExamButton?: boolean
  }>(),
  {
    basePath: '/app/student/chat',
    showExamButton: true,
  },
)

const emit = defineEmits<{
  'session-updated': []
}>()

const router = useRouter()

const messages = ref<DisplayMessage[]>([])
const currentSessionId = ref<number | undefined>(props.sessionId)
const streaming = ref(false)
const sessionTitle = ref<string | null>(null)
const sessionSubject = ref<string | null>(null)
const sessionClassName = ref<string | null>(null)
const attachedFiles = ref<{ filename: string; chunks: number }[]>([])
const uploadError = ref<string | null>(null)
const loadError = ref<string | null>(null)
const loadingHistory = ref(false)
const confirmingDelete = ref(false)
const deleting = ref(false)
let confirmDeleteTimeout: ReturnType<typeof setTimeout> | null = null

const messagesEnd = ref<HTMLElement | null>(null)

function scrollToBottom() {
  nextTick(() => messagesEnd.value?.scrollIntoView({ block: 'end' }))
}

async function initSession(id: number | undefined) {
  // Already have this session's data locally (e.g. we just streamed the first
  // exchange and updated the URL ourselves) — skip the redundant refetch.
  if (id === currentSessionId.value && messages.value.length > 0) return

  messages.value = []
  currentSessionId.value = id
  sessionTitle.value = null
  sessionSubject.value = null
  sessionClassName.value = null
  attachedFiles.value = []
  loadError.value = null
  confirmingDelete.value = false
  if (confirmDeleteTimeout) clearTimeout(confirmDeleteTimeout)

  if (id == null) return

  loadingHistory.value = true
  try {
    const [transcript, sessions] = await Promise.all([chatApi.getSessionMessages(id), chatApi.getSessions()])
    messages.value = transcript.map((m) => ({
      role: m.role === 'user' ? 'user' : 'assistant',
      content: m.content,
      subject: m.subject,
      topic: m.topic,
    }))
    const match = sessions.find((s) => s.id === id)
    if (match) {
      sessionTitle.value = match.title
      sessionSubject.value = match.subject
      sessionClassName.value = match.className
    }
    scrollToBottom()
  } catch (err) {
    const apiError = err as ApiError
    loadError.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not load this chat.'
  } finally {
    loadingHistory.value = false
  }
}

watch(() => props.sessionId, initSession, { immediate: true })

async function sendMessage(text: string) {
  if (streaming.value) return

  messages.value.push({ role: 'user', content: text })
  const assistantMsg: DisplayMessage = { role: 'assistant', content: '', streaming: true }
  messages.value.push(assistantMsg)
  streaming.value = true
  scrollToBottom()

  try {
    for await (const frame of chatApi.streamChatMessage(text, currentSessionId.value)) {
      if (frame.kind === 'session') {
        const isNewChat = props.sessionId == null && currentSessionId.value == null
        currentSessionId.value = frame.sessionId
        if (isNewChat) {
          router.replace(`${props.basePath}/${frame.sessionId}`)
        }
      } else if (frame.kind === 'token') {
        assistantMsg.content += frame.token
        scrollToBottom()
      } else if (frame.kind === 'done') {
        assistantMsg.scene = frame.scene
        assistantMsg.streaming = false
      } else if (frame.kind === 'meta') {
        sessionTitle.value = frame.title
        sessionSubject.value = frame.subject
        sessionClassName.value = frame.className
        emit('session-updated')
      }
    }
  } catch {
    assistantMsg.content = assistantMsg.content || 'Something went wrong while generating a response. Please try again.'
    assistantMsg.streaming = false
  } finally {
    streaming.value = false
    scrollToBottom()
  }
}

async function requestPracticeQuestions(index: number) {
  const msg = messages.value[index]
  const userMsg = messages.value[index - 1]
  if (!msg || msg.role !== 'assistant' || msg.loadingPracticeQuestions || msg.practiceQuestions) return
  if (!userMsg || userMsg.role !== 'user') return

  msg.loadingPracticeQuestions = true
  try {
    msg.practiceQuestions = await studentApi.getPracticeQuestions(userMsg.content, msg.content)
  } catch {
    msg.practiceQuestions = []
  } finally {
    msg.loadingPracticeQuestions = false
  }
}

async function handleUpload(file: File) {
  uploadError.value = null
  try {
    const res = await chatApi.uploadChatFile(file)
    attachedFiles.value.push({ filename: file.name, chunks: res.chunks })
  } catch (err) {
    const apiError = err as ApiError
    uploadError.value = apiError.messages?.[0] ?? apiError.message ?? 'Could not upload file.'
  }
}

function goToExams() {
  router.push({ path: '/app/student/exams', query: sessionSubject.value ? { topic: sessionSubject.value } : {} })
}

async function handleDeleteClick() {
  if (!currentSessionId.value || deleting.value) return

  if (!confirmingDelete.value) {
    confirmingDelete.value = true
    confirmDeleteTimeout = setTimeout(() => {
      confirmingDelete.value = false
    }, 4000)
    return
  }

  if (confirmDeleteTimeout) clearTimeout(confirmDeleteTimeout)
  deleting.value = true
  try {
    await chatApi.deleteSession(currentSessionId.value)
    emit('session-updated')
    router.push(props.basePath)
  } catch {
    confirmingDelete.value = false
    deleting.value = false
  }
}
</script>

<template>
  <div class="chat-view">
    <header class="chat-header">
      <div class="header-titles">
        <h1 class="chat-title">{{ sessionTitle ?? 'New Chat' }}</h1>
        <div v-if="sessionSubject || sessionClassName" class="breadcrumb">
          <span v-if="sessionSubject" class="subject-pill">{{ sessionSubject }}</span>
          <span v-if="sessionClassName" class="class-label">{{ sessionClassName }}</span>
        </div>
      </div>
      <div class="header-actions">
        <button
          v-if="currentSessionId"
          type="button"
          class="delete-btn"
          :class="{ confirming: confirmingDelete }"
          :disabled="deleting"
          @click="handleDeleteClick"
        >
          {{ deleting ? 'Deleting…' : confirmingDelete ? 'Click to confirm' : 'Delete chat' }}
        </button>
        <button v-if="showExamButton" type="button" class="exam-btn" @click="goToExams">Generate mock exam ↗</button>
      </div>
    </header>

    <div class="chat-body">
      <div v-if="loadingHistory" class="state-msg">Loading…</div>
      <div v-else-if="loadError" class="state-msg error">{{ loadError }}</div>
      <template v-else>
        <p v-if="messages.length === 0" class="empty-state">Ask a question to get started.</p>
        <MessageBubble
          v-for="(m, i) in messages"
          :key="i"
          :role="m.role"
          :content="m.content"
          :subject="m.subject"
          :topic="m.topic"
          :scene="m.scene"
          :streaming="m.streaming"
          :practice-questions="m.practiceQuestions"
          :loading-practice-questions="m.loadingPracticeQuestions"
          @request-practice-questions="requestPracticeQuestions(i)"
        />
        <div ref="messagesEnd" />
      </template>
    </div>

    <div class="chat-footer">
      <AttachedFileChip v-for="f in attachedFiles" :key="f.filename" :filename="f.filename" :chunks="f.chunks" />
      <p v-if="uploadError" class="upload-error">{{ uploadError }}</p>
      <ChatComposer :disabled="streaming" @send="sendMessage" @attach="handleUpload" />
    </div>
  </div>
</template>

<style scoped>
.chat-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  max-width: 1400px;
}

.chat-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line);
  margin-bottom: 20px;
}

.header-titles {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.chat-title {
  margin: 0;
  font-family: var(--font-heading);
  font-size: 24px;
  color: var(--ink);
}

.breadcrumb {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--muted);
}

.subject-pill {
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--green-deep);
  background: var(--sage-soft);
  border-radius: 999px;
  padding: 3px 10px;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 0 0 auto;
}

.exam-btn,
.delete-btn {
  flex: 0 0 auto;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  background: var(--white);
  color: var(--ink-2);
  font-family: var(--font-heading);
  font-size: 13px;
  font-weight: 600;
  padding: 9px 14px;
  cursor: pointer;
  white-space: nowrap;
}

.exam-btn:hover {
  border-color: var(--green-br);
}

.delete-btn:hover {
  border-color: var(--t-lit);
  color: var(--t-lit);
}

.delete-btn.confirming {
  border-color: var(--t-lit);
  background: var(--t-lit);
  color: var(--white);
}

.delete-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.chat-body {
  flex: 1;
  overflow-y: auto;
  padding-right: 4px;
}

.state-msg {
  font-size: 14px;
  color: var(--muted);
}

.state-msg.error {
  color: var(--t-lit);
}

.empty-state {
  font-size: 14px;
  color: var(--muted);
}

.chat-footer {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-top: 16px;
}

.upload-error {
  margin: 0;
  font-size: 13px;
  color: var(--t-lit);
}
</style>
