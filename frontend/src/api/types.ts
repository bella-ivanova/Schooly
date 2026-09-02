export type UserRole = 'student' | 'teacher' | 'schooladmin' | 'admin'

export interface UserSummary {
  id: string
  username: string
  email: string
  fullName: string
  role: UserRole
  grade: number | null
  schoolName: string | null
  classLetter: string | null
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
  teacherRegistrationCode?: string
  classJoinCode?: string
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

export interface UpdateProfileRequest {
  fullName: string
  grade?: number
}

export interface ChangePasswordRequest {
  currentPassword: string
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
  | { kind: 'done'; scene: string | null }
  | { kind: 'meta'; title: string; subject: string; classId: number | null; className: string | null }

export interface ChatSessionSummary {
  id: number
  title: string | null
  subject: string | null
  classId: number | null
  className: string | null
  createdAt: string
  lastMessageAt: string
}

export interface ChatSessionMessage {
  role: string
  content: string
  subject: string | null
  topic: string | null
  timestamp: string
}

export interface ChatUploadResponse {
  chunks: number
}

export interface SceneHtmlResponse {
  html: string
}

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

export interface StudentClassEntry {
  classId: number
  className: string
}

export interface StudentClassesInfo {
  schoolId: number | null
  schoolName: string | null
  classes: StudentClassEntry[]
}

export interface GenerateExamResponse {
  id: number
  exam: string
}

export interface SavedExamSummary {
  id: number
  topic: string
  createdAt: string
}

export interface SavedExamDetail {
  id: number
  topic: string
  content: string
  createdAt: string
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

export interface TeacherRosterStudent {
  id: string
  username: string
  fullName: string
  grade: number | null
}

export interface TeacherStudentSummary {
  id: string
  username: string
  fullName: string
  grade: number | null
  classNames: string[]
}

export interface TeacherStudentWeakSpot {
  topic: string
  subject: string
  count: number
}

export interface TeacherStudentStats {
  questionCount30d: number
  lastActiveAt: string | null
  savedExamCount: number
  weakSpots: TeacherStudentWeakSpot[]
}

// ── SchoolAdmin dashboard ────────────────────────────────────────────────

export interface AdminClassSummary {
  id: number
  name: string
  subjectId: number | null
  subjectName: string | null
  homeroomTeacherUsername: string | null
  studentCount: number
}

export interface AdminSubjectSummary {
  id: number
  name: string
}

export interface SchoolInfo {
  id: number
  name: string | null
}

export interface SchoolTeacherCode {
  code: string
  createdAt: string
}

export interface ClassJoinCode {
  code: string
  createdAt: string
}

export interface AdminUserSummary {
  id: string
  username: string
  fullName: string
  role: string
  grade: number | null
  classNames: string[]
}

export interface ClassTeacherAssignment {
  teacherId: string
  teacherUsername: string
  subjectId: number
  subjectName: string
}

export interface ClassRosterStudent {
  id: string
  username: string
  fullName: string
}

export interface AdminClassDetail {
  id: number
  name: string
  subjectId: number | null
  subjectName: string | null
  homeroomTeacherUsername: string | null
  students: ClassRosterStudent[]
  teacherAssignments: ClassTeacherAssignment[]
}

// ── GlobalAdmin dashboard ────────────────────────────────────────────────

export interface SchoolSummary {
  id: number
  name: string
  createdAt: string
  studentCount: number
  teacherCount: number
}
