export type UserRole = 'student' | 'teacher' | 'schooladmin' | 'admin'

export interface UserSummary {
  id: string
  username: string
  email: string
  fullName: string
  role: UserRole
  grade: number | null
}

export interface LoginRequest {
  usernameOrEmail: string
  password: string
}

export interface RegisterRequest {
  username: string
  email: string
  password: string
  fullName: string
  role: 'student' | 'teacher'
  grade?: number
  classLetter?: string
  schoolId?: number
  teacherRegistrationCode?: string
}

export interface AuthResponse {
  token: string
  refreshToken: string
  user: UserSummary
}

export interface RefreshRequest {
  refreshToken: string
}

export interface RefreshResponse {
  token: string
  refreshToken: string
}

export interface ForgotPasswordRequest {
  email: string
}

export interface VerifyResetCodeRequest {
  email: string
  code: string
}

export interface ResetPasswordRequest {
  email: string
  code: string
  newPassword: string
}

export interface ApiError {
  status: number
  message: string
  messages: string[]
}

export interface ChatMessageRequest {
  message: string
  sessionId?: number
}

export type ChatSseFrame =
  | { kind: 'session'; sessionId: number }
  | { kind: 'token'; token: string }
  | { kind: 'done'; scene: unknown | null }
  | { kind: 'meta'; title: string; subject: string }

// ── Student dashboard ────────────────────────────────────────────────────

export interface WeakSpot {
  topic: string
  subject: string
  count: number
}

export interface HistoryMessage {
  role: string
  content: string
  subject: string
  topic: string
  timestamp: string
}

// ── Teacher dashboard ────────────────────────────────────────────────────

export interface TeacherClassInfo {
  id: number
  name: string
  school: string
}

export interface TeacherSubjectInfo {
  id: number
  name: string
  school: string
}

export interface TeacherClassSummary {
  class: TeacherClassInfo
  subjects: TeacherSubjectInfo[]
  studentCount: number
}

export interface TeacherStruggleGroup {
  class: TeacherClassInfo
  subject: { id: number; name: string }
  topTopics: { topic: string; count: number }[]
}

export interface TeacherActivityStudent {
  id: string
  username: string
  fullName: string
  grade: number | null
}

export interface TeacherActivityEntry {
  class: TeacherClassInfo
  topStudents: { student: TeacherActivityStudent; questionCount: number }[]
}

// ── SchoolAdmin dashboard ────────────────────────────────────────────────

export interface AdminClassSummary {
  id: number
  name: string
  homeroomTeacherUsername: string | null
  studentCount: number
}

export interface SchoolInfo {
  id: number
  name: string | null
}

// ── GlobalAdmin dashboard ────────────────────────────────────────────────

export interface SchoolSummary {
  id: number
  name: string
  createdAt: string
  studentCount: number
  teacherCount: number
}
