# PRD — AI-Powered Tutoring Chat

**One-liner:** A curriculum-grounded chat interface that lets students ask questions and receive answers restricted to the material they have studied at their grade level.

---

## Behavioral Specification

**When a student sends a message:**
The system embeds the query, searches Qdrant filtered to grades 1 through N (where N is the student's enrolled grade), and injects the top-k matching curriculum chunks as context before calling the LLM. The LLM is instructed to answer only from the provided context.

**When the LLM response contains a `<STEREO>…</STEREO>` block:**
The system extracts the JSON scene description and returns it as a structured `scene` field alongside the text response so the frontend can render an interactive 3D geometry visualisation.

**When a student asks a question outside their curriculum:**
If Qdrant returns no relevant chunks, the LLM is called with an empty context and a refusal system prompt. It responds that it can only help with material the student has studied — it does not answer from general knowledge.

**When a student uploads a PDF:**
The file is ingested into a temporary session-only vector store (not persisted to the shared Qdrant collection). Subsequent queries in the same session include results from this temporary store alongside grade-filtered curriculum results.

**When the LLM response is slow:**
Responses stream token-by-token to the frontend via Server-Sent Events or chunked transfer encoding so the student sees partial output in real time.

**When the student's JWT expires mid-session:**
The client silently exchanges the refresh token for a new JWT and retries the request. The student never sees an auth error during normal use.

**After every message exchange:**
Both the user message and the assistant response are persisted to the `chat_messages` table. Each record is tagged with a detected subject (linked to a `Subject` entity) and a topic string produced by a one-shot LLM classification call.

---

## Acceptance Criteria

- [ ] `POST /api/chat/message` exists, requires a valid JWT, and streams the LLM response
- [ ] RAG context is grade-filtered: a grade-8 student never receives chunks from grade-9 or higher material
- [ ] When the LLM output contains a `<STEREO>` block, the response includes a structured `scene` field with the extracted JSON; the text field contains the response with the block removed
- [ ] Off-curriculum questions produce a polite refusal message, not a hallucinated answer
- [ ] `POST /api/chat/upload` accepts a PDF, ingests it into a session-scoped temporary store, and affects all subsequent `/api/chat/message` calls in that session
- [ ] Every chat exchange writes two rows to `chat_messages` — one with `role = "user"`, one with `role = "assistant"` — both with a populated `subject_id` and `topic`
- [ ] The streaming response reaches the client in real time (tokens visible as they are generated, not batched at the end)
- [ ] Token expiry during a session is handled transparently via refresh token exchange on the client side

---

## Edge Cases

**Math notation in messages or PDFs**
Pix2Text handles LaTeX at ingest time for formula-heavy textbook PDFs. The LLM must output math in LaTeX notation so the frontend can render it. No special preprocessing of user query text is needed.

**Grade-1 student**
RAG searches with an upper bound of grade 1 — no cross-grade content can appear. This is the lower bound of the filter and must not be treated as an edge case that skips filtering.

**Corrupt or image-only PDF upload**
The OCR fallback chain runs in order: Pix2Text → vision OCR (Ollama) → PdfPig plain text. If all three return empty text, the upload endpoint returns a clear error message rather than silently storing empty chunks.

**Malformed `<STEREO>` block from LLM**
If the regex extraction in `StereometryService.ExtractSceneJson()` finds no valid JSON, the `scene` field is `null` in the response. The text response is always returned regardless.

**Qdrant returns zero results**
The LLM is still called, but with an empty context block and the curriculum-restriction system prompt. The expected output is a refusal, not an attempt to answer from training data.

**ZhipuAI unavailable**
The system falls back to the local Ollama service. The fallback is transparent — no error is surfaced to the student.

**Message exceeding LLM context window**
Long messages are truncated or chunked before submission. The system must not crash or return a 500; it should truncate with a warning log.

**Bulgarian diacritics and Cyrillic text**
Embedding and LLM services handle UTF-8 natively. The only preprocessing required is the existing `CleanText()` sanitisation in `PDFLoader.cs` (null bytes, control characters). No transliteration or encoding conversion is needed.
