# StudyAssist (Schooly)

A curriculum-aware AI tutoring platform for Bulgarian school students. The AI is restricted to the material the student has already studied at their grade level, preventing it from answering questions outside the curriculum.

## Implementation Status

### Complete
- User authentication: login, register, logout, JWT access tokens + refresh tokens
- Password reset flow: token generation and validation (email delivery is a remaining task)
- Progressive rate limiting: per-account and per-IP for login, per-email for registration and password reset
- User roles: Student, Teacher, SchoolAdmin, Admin
- School class and subject management (CLI-only admin commands via `AdminUserService`)
- PDF ingestion pipeline: triple-OCR fallback (Pix2Text → vision OCR → PdfPig), text chunking, and embedding
- Qdrant vector DB integration with grade-filtered semantic search
- Ollama local LLM chat service with streaming
- ZhipuAI remote LLM chat service with streaming (fallback backend)
- RAG orchestration: grade-filtered retrieval + LLM call with context injection
- Stereometry 3D scene JSON schema and `<STEREO>` block extraction from LLM responses
- Exam generation service: mock exams generated from curriculum material via RAG
- Chat message persistence with subject and topic tagging
- Weak-spot detection: aggregates most-asked topics per student
- Database schema: 9 migrations applied (PostgreSQL)

### In Progress
- Student chat HTTP endpoint (`POST /api/chat/message`) — service layer complete, controller not yet wired
- Teacher dashboard HTTP endpoint — `TeacherDashboardService` exists, not exposed via controller
- School admin HTTP endpoints — all admin operations are currently CLI-only

### Not Started
- Password reset email delivery — currently the reset token is returned in the HTTP response; must switch to email before production
- PDF upload endpoint for students — `TempFileManager` and session-store ingestion exist in `RAGService`, no HTTP route yet
- Practice question HTTP endpoint — `PracticeQuestionService` exists, not wired
- Rate limiting on `POST /api/auth/refresh`
- Frontend application (Vue/React, separate repository, expected at `localhost:3000` or `:5173`)
