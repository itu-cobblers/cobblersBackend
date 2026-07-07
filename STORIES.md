# User Stories

Each story is the unit that drives **one** contract change. Workflow:

> story → agree on the payload together → write it in [CONTRACT.md](CONTRACT.md) → each side builds their half.

Format: **As a** `<role>`, **I want** `<action>`, **so that** `<value>`.
Each story names its **transport** (REST vs SignalR), the **contract artifact** it
needs, and the **open questions** it forces. Keep it light — no points, no sprints.

Ordered by dependency and risk: build top-down.

---

## S1 — Run code ✅ contract defined

**As a** student, **I want** to run my code and see the compile/run result,
**so that** I can experiment and fix mistakes before submitting.

- **Transport:** REST
- **Contract:** [`POST /api/execute`](CONTRACT.md#post-apiexecute)
- **Notes:** stateless; the walking skeleton. No identity, no session needed.
- **Done when:** valid code shows stdout; broken code shows the compile/runtime error.

---

## S2 — Submit a finished task ✅ contract defined

**As a** student, **I want** to submit my code when I've finished a task,
**so that** my progress is recorded.

- **Transport:** REST
- **Contract:** [`POST /api/tasks/{taskId}/submissions`](CONTRACT.md#submission)
- **Introduces:** identity (`studentId`) + persisted progress. Built on top of `execute`.
- **Decisions already made:**
  - Payload is `{ studentId, sessionId?, content }` → returns `{ subId, passed, result, submittedAt }`.
  - "Completed" is decided server-side (`Task.Id` → a backend-owned grading
    lookup for `code`, a generic compare for `predict`), not self-reported by
    the client. See [SCHEMA.md](SCHEMA.md#grading-lives-in-backend-code-not-the-database).
  - Progress is stored in a `Submission` table, keyed by `studentId` — see [SCHEMA.md](SCHEMA.md).
- **Open questions:**
  - [ ] `project` submissions have no automated grader yet (`passed` stays `null`) — manual review path is undecided.
- **Done when:** a submission records the student as having completed that task, and it survives a reload.

---

## S3 — Teacher sets a timer on students' screens

**As a** teacher, **I want** to set a timer that appears on my students' screens,
**so that** I can pace the workshop with a shared countdown.

- **Transport:** SignalR (server → room broadcast) + REST trigger
- **Contract:** [`POST /api/sessions/{code}/timer`](CONTRACT.md#timer-teacher--room-broadcast) → `TimerStarted` event
- **Depends on:** [Sessions / rooms](CONTRACT.md#sessions-rooms) — the timer broadcasts to a room, so
  solo (off-site) students don't receive it.
- **Decisions already made:**
  - Scoped to a **room** (SignalR Group), not all connections.
  - Absolute `endsAt`, not a duration (late joiners / reconnects sync correctly).
  - Non-coercive — a reminder only; nothing is forced if it elapses.
- **Done when:** a timer the teacher starts appears for everyone in the room, including someone who joins mid-countdown, and never appears for solo students.

---

## S4 — Solo student practices without a room

**As a** solo student (never in a teacher's room), **I want** to fetch a task
set and submit my work anyway, **so that** I can keep practicing on my own
after the workshop, or if I'm not attending live.

- **Transport:** REST
- **Contract:** [`GET /api/tasksets/{tasksetId}/tasks`](CONTRACT.md#tasks),
  [`POST /api/tasks/{taskId}/submissions`](CONTRACT.md#submission) with `sessionId` omitted
- **Depends on:** S2, but skips [Sessions / rooms](CONTRACT.md#sessions-rooms)
  entirely — no `Attendance` row is ever created for this population.
- **Decisions already made:**
  - `Submission.sessionId` is nullable — one endpoint serves both populations.
  - The frontend hardcodes which `tasksetId` "practice mode" points at; the
    backend doesn't need to know a student is "solo" beyond the missing `sessionId`.
- **Done when:** a submission with no `sessionId` is accepted, graded the same
  way as a room submission, and shows up in the student's history (S5).

---

## S5 — Student resumes across the 3 days

**As a** student, **I want** my past submissions to still be there when I come
back the next day (or reload), **so that** I don't lose progress or redo
finished tasks.

- **Transport:** REST
- **Contract:** [`GET /api/students/{studentId}/submissions`](CONTRACT.md#submission)
- **Depends on:** S2 (persisted submissions); [Identity](CONTRACT.md#identity-no-registration) (`studentId` survives in `localStorage` across days).
- **Decisions already made:**
  - Completion is derived from `Submission.passed` server-side, not a
    client-side id list — this retires the frontend's old
    active/completed-key hack. See [SCHEMA.md](SCHEMA.md#taskid-is-a-fresh-identity).
- **Open questions:**
  - [ ] A student who loses their `studentId` (new browser/device) has no
    recovery path today — treated as a brand-new student. Accepted risk, not solved.
- **Done when:** a student who reloads, or returns the next day, sees which tasks they already passed.

---

## Backlog (unwritten)

Stub stories — flesh out before building.

- Teacher sees live student progress (`ProgressUpdated` broadcast to the teacher view).
- Student picks a task from the sidebar.
- Student reveals a sample/reference solution for a task. `Task.SampleSolutionJson` exists (see [SCHEMA.md](SCHEMA.md#sample-solution-is-a-separate-column)) but
  what gates revealing it (a passing submission? an explicit "give up"? always available?) and the endpoint shape are both undecided.
- Teacher manually marks a `project` submission as passed/failed (no automated grader exists).
