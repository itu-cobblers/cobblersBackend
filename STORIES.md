# User Stories

Each story is the unit that drives **one** contract change. Workflow:

> story → agree on the payload together → write it in [CONTRACT.md](CONTRACT.md) → each side builds their half.

Format: **As a** `<role>`, **I want** `<action>`, **so that** `<value>`.
Each story names its **transport** (REST vs SignalR), the **contract artifact** it
needs, splits work into **Frontend** / **Backend**, and lists the **open questions**
it forces. Keep it light — no points, no sprints.

Ordered by dependency and risk: build top-down.

---

## S1 — Run code ✅ contract defined, ✅ built

**As a** student, **I want** to run my code and see the compile/run result,
**so that** I can experiment and fix mistakes before submitting.

- **Transport:** REST
- **Contract:** [`POST /api/execute`](CONTRACT.md#post-apiexecute)
- **Frontend:** CodeEditor + Run button call `execute`, render `stdout`/`stderr` off the `status` field.
- **Backend:** `ExecutorController` → `ExecutorService` → `PistonClient`. Built.
- **Notes:** stateless; the walking skeleton. No identity, no session needed.
- **Done when:** valid code shows stdout; broken code shows the compile/runtime error.

---

## S2 — Submit a finished task ✅ contract defined

**As a** student, **I want** to submit my code when I've finished a task,
**so that** my progress is recorded.

- **Transport:** REST
- **Contract:** [`POST /api/tasks/{taskId}/submissions`](CONTRACT.md#submission)
- **Introduces:** identity (`studentId`) + persisted progress. Built on top of `execute`.
- **Frontend:** not built yet — Submit button needs to call the new endpoint and render `passed`/`result` instead of relying on the client-side `check()`.
- **Backend:** not built yet — needs `Task`/`Submission` persistence (see [SCHEMA.md](SCHEMA.md)) and the grading dispatch (`code` lookup / `predict` compare).
- **Decisions already made:**
  - Payload is `{ studentId, sessionId?, content }` → returns `{ subId, passed, result, submittedAt }`.
  - "Completed" is decided server-side (`Task.Id` → a backend-owned grading
    lookup for `code`, a generic compare for `predict`), not self-reported by
    the client. See [SCHEMA.md](SCHEMA.md#grading-rules-are-data-evaluated-by-one-backend-engine).
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
- **Frontend:** teacher-side controls (minutes input + Start) are built (`TeacherDashboard`). Student-side is partially built — `TimerStarted` is received and logged, but there's no visible countdown UI yet.
- **Backend:** `SessionsController.StartTimer` + `SessionHub` broadcast. Built (in-memory `SessionStore` — will move onto persisted `Session` per SCHEMA.md).
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
- **Frontend:** the entry point (join-bar UI) is built — see S7. Actually loading a real taskset's tasks and submitting from solo mode is **not** wired up yet (today, starting Solo Practice only flips UI state; the workspace still reads the local hardcoded `TASKS`).
- **Backend:** not built yet — neither endpoint exists.
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
- **Frontend:** not built yet — fetch history on load, mark completed tasks in the sidebar (replaces the old local active/completed-key logic).
- **Backend:** not built yet — the query itself is straightforward once `Submission` exists.
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

## S6 — Teacher picks a taskset when creating a session

**As a** teacher, **I want** to choose which taskset a new session uses,
**so that** I control what content today's room serves instead of it being implicit.

- **Transport:** REST
- **Contract:** [`GET /api/tasksets`](CONTRACT.md#tasks) (list), [`POST /api/sessions`](CONTRACT.md#sessions-rooms) (now takes `tasksetId`)
- **Frontend:** ✅ built now — `TeacherDashboard` shows a "Task set" `<select>` above "Create session"; Create is disabled until a taskset is chosen. Backed by a **local mock** (`@lib/tasksetApi.fetchTasksets`) that wraps the current hardcoded task bundle as a single entry, so the picker's style and interaction can be reviewed without waiting on the backend.
- **Backend:** doc-only for now (per instruction — no backend code changed). `GET /api/tasksets` doesn't exist yet; `POST /api/sessions` already ignores its request body today, so sending `{ tasksetId }` against the real backend is harmless (non-breaking) until the backend actually reads and persists it.
- **Depends on:** [SCHEMA.md](SCHEMA.md) `TaskSet.DisplayTitle`.
- **Open questions:**
  - [ ] Real `GET /api/tasksets` endpoint + seeding more than one taskset (today, real or mock, there's effectively only one).
- **Done when:** *(frontend, done)* a teacher can see and pick a taskset before creating a session. *(backend, pending)* the choice is actually persisted as `Session.TasksetId`.

---

## S7 — Student chooses Solo Practice on the join bar

**As a** student who can't get to the physical workshop, **I want** a clearly
labeled way to start practicing on my own from the same screen I'd use to
join a room, **so that** I'm not blocked by needing a room code.

- **Transport:** none for this story (pure UI); reuses S4's REST contract once the student actually submits work.
- **Contract:** no new endpoint — S4's `POST /api/tasks/{taskId}/submissions` with `sessionId` omitted.
- **Frontend:** ✅ built now — `JoinRoomBar` gains a `mode: 'join' | 'solo'` toggle. Default view adds a "Solo Practice" link + hover-info icon (native `title`, tooltip: *"This mode is for students who can't join BootIT on site, and want to practice at their own pace."*); switches to a name-only form with a "Start practicing" button and a "← Join a class instead" link back. Existing name+code join flow is unchanged.
- **Backend:** none — no contract change.
- **Depends on:** S4 (the underlying submission contract).
- **Open questions:**
  - [ ] `handleStartSolo` currently only flips UI state (shows "Practicing solo as {name}"). Wiring it to actually load a taskset's tasks (S4) is separate follow-on work.
- **Done when:** a student can reach a working state without a room code, using a name only, without disrupting the existing join-by-code flow.

---

## S8 — Student reveals a task's sample solution

**As a** student, **I want** to see a reference solution for a task after
I've attempted it, **so that** I can learn from it if I'm stuck or want to
compare approaches.

- **Transport:** REST
- **Contract:** [`GET /api/tasks/{taskId}/solution?studentId=...`](CONTRACT.md#solution)
- **Decisions already made:** the gate is identical for solo and classroom —
  available once **at least one `Submission` exists** for `(studentId,
  taskId)`, pass or fail. A teacher-configurable reveal delay was considered
  and rejected: it would need per-student-per-task timers (students in a room
  don't progress in lockstep), plus teacher-facing controls, for marginal
  benefit over the gate that already exists. See
  [SCHEMA.md](SCHEMA.md#sample-solution-reveal-uses-one-rule-for-both-solo-and-classroom).
- **Frontend:** not built yet — "Show solution" button should be disabled
  until the open task has ≥1 submission, with a hover explaining why (same
  pattern as S7's info icon).
- **Backend:** not built yet — endpoint doesn't exist; `Task.SampleSolutionJson` doesn't exist either (see SCHEMA.md).
- **Done when:** not built — this story's contract is decided, nothing is implemented.

---

## S9 — Student sees a "welcome back" resume prompt

**As a** returning student, **I want** to be offered today's session
automatically if I attended yesterday's, **so that** I don't have to ask the
teacher for the code again or retype it.

- **Transport:** REST
- **Contract:** [`GET /api/students/{studentId}/resume-suggestion`](CONTRACT.md#resume-suggestion-planned) — **plan only, not implemented.**
- **Depends on:** [Identity](CONTRACT.md#identity-no-registration) (persistent `studentId`); [Sessions](CONTRACT.md#sessions-rooms) (`Attendance` history).
- **Decisions already made:** matching heuristic is "the most recently
  created `Session` this student doesn't already have an `Attendance` row
  for" — no course/cohort entity needed, since the app assumes one active
  class at a time. See [SCHEMA.md](SCHEMA.md#welcome-back-resume-suggestion-needs-no-new-schema).
- **Open questions:**
  - [ ] Prompt UI/component not designed — CONTRACT.md defines the data, not the component.
  - [ ] Tie-break if more than one session was created "today" (most recent `CreateDatetime` — see SCHEMA.md open decisions).
- **Done when:** not started — this story is a written-down plan so both
  sides agree on the approach ahead of time; nothing is built yet.

---

## S10 — Teacher re-syncs full attendance + progress on (re)connect

**As a** teacher, **I want** to see everyone who joined and how far each got
after I reload, reconnect, or the server restarts, **so that** I don't lose
the class's state just because a connection dropped.

- **Transport:** REST (hydration) + SignalR (live deltas — existing `StudentJoined`, plus a backlog `ProgressUpdated`)
- **Contract:** [`GET /api/sessions/{code}/attendance`](CONTRACT.md#teacher-dashboard-hydration-attendance--progress), [`GET /api/sessions/{code}/progress`](CONTRACT.md#teacher-dashboard-hydration-attendance--progress)
- **Frontend:** not built — on dashboard load, hydrate the roster from `/attendance` and pass status from `/progress`, **then** start `ObserveSession` for live `StudentJoined`. Today the dashboard has only the live layer, so a reconnecting teacher sees just who's currently connected — anyone who stepped away has vanished.
- **Backend:** not built — needs persisted `Attendance` + `Submission` (see [SCHEMA.md](SCHEMA.md)); the queries themselves are straightforward reads.
- **Depends on:** S2 (persisted submissions); persisted Sessions / Attendance (SCHEMA.md).
- **Decisions already made:**
  - Live roster (`ObserveSession`) and persisted attendance are **different reads** — the first is who's connected now, the second is who attended. Don't conflate them; the teacher side needs a REST hydration layer mirroring the student side's `SessionState`.
  - "passed" is per-(student, task) EXISTS a passing submission, **not** a row average; `project` tasks (`passed = null`) are excluded from pass lists. See [CONTRACT.md](CONTRACT.md#teacher-dashboard-hydration-attendance--progress).
- **Open questions:**
  - [ ] Live per-task progress push (`ProgressUpdated`) so the dashboard updates without re-fetching — still backlog.
- **Done when:** a teacher who reloads mid-class sees the full roster of everyone who joined **and** each student's passed tasks, not just who's currently connected.

---

## Backlog (unwritten)

Stub stories — flesh out before building.

- Teacher sees live student progress via a `ProgressUpdated` broadcast (the live-delta half of S10 — hydration is defined; the push is not).
- Student picks a task from the sidebar.
- Teacher manually marks a `project` submission as passed/failed (no automated grader exists).
