# Generated Test Accounts (QA Load Test, 2026-09-11)

Created during a heavy-load functional test pass to safely exercise destructive/
mutating admin endpoints without touching the pre-existing seeded `test.*`
accounts or "Test School" (id 3) documented in `README.md`. These live only in
the local dev Postgres DB used at the time — not reproducible from a fresh DB.

**Do not commit real production credentials to git using this pattern** — these
are local-dev-only accounts with throwaway passwords, kept here for reuse in
future local test sessions per explicit request.

## School
- **QA Load Test School** — id `6`
- Orphaned duplicate: **QA Load Test School 2** — id `7`. Created accidentally
  (the first `POST /api/global-admin/schools` call returned `201` with an empty
  body, so a retry was issued that also succeeded). Harmless — there is no
  `DELETE /api/global-admin/schools/{id}` endpoint in this app, so this orphan
  is permanent unless removed directly in Postgres.

## Accounts

| Role | Username | Email | Password | Notes |
|---|---|---|---|---|
| SchoolAdmin | `qa_load_admin_1789112739` | `qa.load.admin.1789112739@studyassist.test` | `QaLoad2026!` | Registered as a plain schoolless Student, then promoted to SchoolAdmin of School id 6 via `PUT /api/global-admin/users/{id}/role`. User id `169e4ca6-17e0-423e-802b-06e581fd2d42`. |
| Teacher | `qa_load_teacher_1789112739` | `qa.load.teacher.1789112739@studyassist.test` | `QaLoad2026!` | Registered via a real `SchoolTeacherCode` for School id 6. User id `1df50e85-2537-43a5-b297-fc701d0bdb95`. Currently has **no class assignment and no `TeacherSubject` qualification** — the one throwaway class it was assigned to (`QA-10A`, id 8) was deleted during destructive-endpoint testing. |
| Student | `qa_load_student_1789112739` | `qa.load.student.1789112739@studyassist.test` | `QaLoad2026New!` (changed from `QaLoad2026!` during a change-password test) | Full name updated to "QA Load Student Updated", grade updated to 11 (via `PUT /api/auth/profile`). Currently **not in any class** — was self-joined into `QA-10A` via class-join-code, then removed via `DELETE /api/admin/classes/{classId}/students/{userId}`, then the class itself was deleted. Still belongs to School id 6. User id `5cc52a8e-4a78-41b2-a477-b24182e1212a`. |

## Current live artifacts for reuse
- School id 6 ("QA Load Test School") has **no subjects and no classes** right
  now — both were deleted during the destructive-endpoint test pass (`QA
  Physics` subject id 31, `QA-10A` class id 8).
- School id 6's `SchoolTeacherCode` was regenerated during testing; the code
  active at the end of this session was **not recorded** deliberately (codes
  are meant to be regenerated on demand — call `GET /api/admin/teacher-code` or
  `POST /api/admin/teacher-code/regenerate` as the SchoolAdmin above to get a
  current one).

## Reuse tips
- Log in as the SchoolAdmin above to create a fresh subject/class/join-code
  chain in School id 6 without touching the original seeded "Test School".
- A second throwaway teacher registration attempt with an already-superseded
  teacher code (`qa_should_fail` / `qa.should.fail@studyassist.test`) was
  intentionally left **unregistered** — that call correctly failed with
  `401 {"error":"Invalid school registration code."}` and no account was
  created.
