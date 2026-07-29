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

## S2 — Submit a finished assignment ✅ contract defined

**As a** student, **I want** to submit my code when I've finished an assignment,
**so that** my progress is recorded.

- **Transport:** REST
- **Contract:** [`POST /api/assignments/{assignmentId}/submissions`](CONTRACT.md#submission)
- **Introduces:** identity (`studentId`) + persisted progress. Built on top of `execute`.
- **Frontend:** ✅ built — Submit (for `code`) and predict's answer submit both call `submitAssignment` (`@lib/submissionApi`) against the real endpoint and render `passed`/`result`, not a client-side `check()`/quiz-check call. `studentId` is registered server-side (`PUT /api/students/{studentId}`, `@lib/studentApi`) before the first submission of a session, since `SubmissionService` rejects an unrecognized `studentId`.
- **Backend:** ✅ built — `SubmissionController` → `SubmissionService`, `Assignment`/`Submission` persistence (see [SCHEMA.md](SCHEMA.md)) and the grading dispatch (`code`/`predict` via `GradingJson`).
- **Decisions already made:**
  - Payload is `{ studentId, sessionId?, content }` → returns `{ subId, passed, result, submittedAt }`.
  - "Completed" is decided server-side (`Assignment.Id` → a backend-owned grading
    lookup for `code`, a generic compare for `predict`), not self-reported by
    the client. See [SCHEMA.md](SCHEMA.md#grading-rules-are-data-evaluated-by-one-backend-engine).
  - Progress is stored in a `Submission` table, keyed by `studentId` — see [SCHEMA.md](SCHEMA.md).
- **Decided:** `project` doesn't submit through this endpoint at all — see
  [CONTRACT.md](CONTRACT.md#mini-projects-are-vs-code-only). `code`/`predict`
  are the only kinds this endpoint needs to handle; the old open question
  about a manual-review path for `project` is moot.
- **Done when:** a submission records the student as having completed that assignment, and it survives a reload.

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

**As a** solo student (never in a teacher's room), **I want** to fetch an assignment
set and submit my work anyway, **so that** I can keep practicing on my own
after the workshop, or if I'm not attending live.

- **Transport:** REST
- **Contract:** [`GET /api/assignmentsets/{assignmentSetId}/assignments`](CONTRACT.md#assignments),
  [`POST /api/assignments/{assignmentId}/submissions`](CONTRACT.md#submission) with `sessionId` omitted
- **Frontend:** the entry point (join-bar UI) is built — see S7. ✅ Solo Practice now loads the real solo set (`all-assignments-for-solo-2026`) from `GET /api/assignmentsets/{id}/assignments` (branch `feat/taskAPI`). Submitting from solo mode is still **not** wired up.
- **Backend:** the assignment-fetch endpoint is built (under the old task naming — rename pending); the submissions endpoint doesn't exist yet.
- **Depends on:** S2, but skips [Sessions / rooms](CONTRACT.md#sessions-rooms)
  entirely — no `Attendance` row is ever created for this population.
- **Decisions already made:**
  - `Submission.sessionId` is nullable — one endpoint serves both populations.
  - The frontend hardcodes which `assignmentSetId` "practice mode" points at; the
    backend doesn't need to know a student is "solo" beyond the missing `sessionId`.
- **Done when:** a submission with no `sessionId` is accepted, graded the same
  way as a room submission, and shows up in the student's history (S5).

---

## S5 — Student resumes across the 3 days

**As a** student, **I want** my past submissions to still be there when I come
back the next day (or reload), **so that** I don't lose progress or redo
finished assignments.

- **Transport:** REST
- **Contract:** [`GET /api/students/{studentId}/submissions`](CONTRACT.md#submission)
- **Frontend:** ✅ built now — `useStudentSession` fetches history on load (independent of join/solo/entry screen) and seeds `useAssignments`'s completed-assignment set from it (replaces the old local active/completed-key logic), so the stepper shows yesterday's passes without a fresh submission this session. A dedicated **"My Progress"** panel (`ProgressModal`, opened from the Toolbar or a link on the entry screen) lists every catalog assignment with every attempt ever made, grouped by assignment — an assignment shows *Passed* if **any** attempt passed (not the latest, not an average); `project`-kind assignments are excluded (they never submit — see [Mini-projects are VS-Code-only](CONTRACT.md#mini-projects-are-vs-code-only)). Calls the endpoint defensively: any failure (it doesn't exist yet) resolves to `[]`, so the rest of the app behaves as if the student has no history yet, not broken.
- **Backend:** not built yet — the query itself is straightforward once `Submission` exists; see [SCHEMA.md](SCHEMA.md#submission) for the exact shape (filter + order by `SubmittedAt` desc, join `Session` for the wire `sessionId`/code).
- **Depends on:** S2 (persisted submissions); [Identity](CONTRACT.md#identity-no-registration) (`studentId` survives in `localStorage` across days).
- **Decisions already made:**
  - Completion is derived from `Submission.passed` server-side, not a
    client-side id list — this retires the frontend's old
    active/completed-key hack. See [SCHEMA.md](SCHEMA.md#assignmentid-is-a-fresh-identity).
  - An assignment's status is "passed" if **any** submission for it passed — the frontend still lists every attempt (My Progress), it just doesn't gate "done" on the *latest* one.
- **Open questions:**
  - [ ] A student who loses their `studentId` (new browser/device) has no
    recovery path today — treated as a brand-new student. Accepted risk, not solved.
- **Done when:** a student who reloads, or returns the next day, sees which assignments they already passed. Frontend: done. Backend: pending.

---

## S6 — Teacher picks an assignment set when creating a session

**As a** teacher, **I want** to choose which assignment set a new session uses,
**so that** I control what content today's room serves instead of it being implicit.

- **Transport:** REST
- **Contract:** [`GET /api/assignmentsets`](CONTRACT.md#assignments) (list), [`POST /api/sessions`](CONTRACT.md#sessions-rooms) (now takes `assignmentSetId`)
- **Frontend:** ✅ built now — `TeacherDashboard` shows an "Assignment set" `<select>` above "Create session"; Create is disabled until an assignment set is chosen. The picker calls the **real API** (`@lib/assignmentSetApi.fetchAssignmentSets`, branch `feat/taskAPI`) — the local mock is gone.
- **Backend:** ✅ built under the old task naming (route/field rename pending). `POST /api/sessions` **requires** `{ assignmentSetId }` and rejects unknown ids with `400` (see CONTRACT.md).
- **Depends on:** [SCHEMA.md](SCHEMA.md) `AssignmentSet.DisplayTitle`.
- **Open questions:**
  - [ ] Seeding more than one assignment set (today there's effectively only one plus the solo set).
- **Done when:** ✅ a teacher can see and pick an assignment set before creating a session, and the choice is persisted as `Session.AssignmentSetId`. Remaining: the naming rename on routes/DTOs.

---

## S7 — Student chooses Solo Practice on the join bar

**As a** student who can't get to the physical workshop, **I want** a clearly
labeled way to start practicing on my own from the same screen I'd use to
join a room, **so that** I'm not blocked by needing a room code.

- **Transport:** none for this story (pure UI); reuses S4's REST contract once the student actually submits work.
- **Contract:** no new endpoint — S4's `POST /api/assignments/{assignmentId}/submissions` with `sessionId` omitted.
- **Frontend:** ✅ built now — `JoinRoomBar` gains a `mode: 'join' | 'solo'` toggle. Default view adds a "Solo Practice" link + hover-info icon (native `title`, tooltip: *"This mode is for students who can't join BootIT on site, and want to practice at their own pace."*); switches to a name-only form with a "Start practicing" button and a "← Join a class instead" link back. Existing name+code join flow is unchanged.
- **Backend:** none — no contract change.
- **Depends on:** S4 (the underlying submission contract).
- **Open questions:**
  - [x] `handleStartSolo` now loads the real solo assignment set from the API (see S4) instead of only flipping UI state.
- **Done when:** a student can reach a working state without a room code, using a name only, without disrupting the existing join-by-code flow.

---

## S8 — Student reveals an assignment's sample solution

**As a** student, **I want** to see a reference solution for an assignment after
I've attempted it, **so that** I can learn from it if I'm stuck or want to
compare approaches.

- **Transport:** REST
- **Contract:** [`GET /api/assignments/{assignmentId}/solution?studentId=...`](CONTRACT.md#solution)
- **Decisions already made:** the gate is identical for solo and classroom —
  available once **at least one `Submission` exists** for `(studentId,
  assignmentId)`, pass or fail. A teacher-configurable reveal delay was considered
  and rejected: it would need per-student-per-assignment timers (students in a room
  don't progress in lockstep), plus teacher-facing controls, for marginal
  benefit over the gate that already exists. See
  [SCHEMA.md](SCHEMA.md#sample-solution-reveal-uses-one-rule-for-both-solo-and-classroom).
- **Frontend:** not built yet — "Show solution" button should be disabled
  until the open assignment has ≥1 submission, with a hover explaining why (same
  pattern as S7's info icon).
- **Backend:** not built yet — endpoint doesn't exist; `Assignment.SampleSolutionJson` doesn't exist either (see SCHEMA.md).
- **Done when:** not built — this story's contract is decided, nothing is implemented.

---

## S9 — Student joins today's session with one click

**As a** student, **I want** the entry screen to tell me if a session is
currently running and let me join it without typing a code, **so that** I
don't have to ask the teacher to repeat it or copy it down.

> **History:** originally scoped as a *personalized* "welcome back" resume
> prompt (`GET /api/students/{studentId}/resume-suggestion`, matched against
> this student's own `Attendance` history, surfaced via a dismissible
> `WelcomeBackBanner`). That plan was **retired before the backend route was
> built** in favor of the simpler version below — see
> [CONTRACT.md](CONTRACT.md#resume-suggestion-retired) for why the
> personalization wasn't worth its complexity.

- **Transport:** REST
- **Contract:** [`GET /api/sessions/today-latest`](CONTRACT.md#get-apisessionstoday-latest-student-entry-screen--is-a-session-live-today)
- **Frontend:** ✅ built — the entry screen (`EntryPortal`, orchestrated by
  `useStudentSession`) fetches this on mount, with no `studentId` involved.
  A "Join current Session" button shows the session `code` inline and is
  enabled once a name is typed and a session is found; it reads "No current
  active session to join" (disabled) if none is, and "Checking for a
  session…" (disabled) while the request is in flight. Clicking it joins via
  the existing `JoinSession` hub path — no manual code entry, and no more
  free-text code input on the entry screen at all. Any non-2xx/network error
  degrades to "no session today," never a blocking error.
- **Backend:** ✅ built — `SessionService.GetTodayLatestActiveSessionAsync`
  (most recent `active` `Session` with `CreateAt` on or after today's UTC
  midnight), exposed via `SessionsController`.
- **Depends on:** [Sessions](CONTRACT.md#sessions-rooms) (`Session.CreateAt` + the new `Status`, see [SCHEMA.md](SCHEMA.md#sessionstatus)).
- **Decisions already made:**
  - No personalization, no `Attendance` join — "is a session live today" has
    the same answer for every student, so it doesn't need `studentId`.
  - Tie-break if more than one session was created "today": most recent
    `CreateAt`, filtered to `Status = active` — see [SCHEMA.md → Open decisions](SCHEMA.md#open-decisions).
- **Done when:** a student who lands on the entry screen while a session is
  live sees an enabled "Join current Session ⟨code⟩" button and can join
  without ever seeing or typing a code; with none live, the button explains
  why it's disabled instead of just being greyed out silently.

---

## S12 — Teacher manually ends a session

**As a** teacher, **I want** to explicitly end today's session, **so that**
it stops showing up as joinable and every student currently in the room is
sent back to the entry screen.

- **Transport:** REST (trigger) + SignalR (fan-out, same split as the timer)
- **Contract:** [`POST /api/sessions/{code}/end`](CONTRACT.md#post-apisessionscodeend) → `SessionEnded` event
- **Frontend:** ✅ built — the teacher dashboard's existing "End session"
  action (`useTeacherSession`) now calls the real endpoint (`@lib/sessionApi.endSession`)
  instead of only clearing local state, with an `isEndingSession` loading
  state on the button. Students receive `SessionEnded` in `useStudentSession`
  and are bounced back to the entry screen — the same teardown path as if
  they'd never joined, not an error dialog.
- **Backend:** ✅ built — `SessionsController.EndSession` sets
  `Session.Status = ended`, clears the room's `SessionStore` entry
  (`RemoveRoom`), and broadcasts `SessionEnded` to Group `code`. `GET /api/sessions/{code}`
  and `GET /api/sessions/today-latest` both stop returning the session
  immediately afterward (see [SCHEMA.md](SCHEMA.md#sessionstatus)).
- **Depends on:** [Sessions](CONTRACT.md#sessions-rooms); [S9](#s9--student-joins-todays-session-with-one-click) (today-latest must respect `Status` or an ended room would still look joinable).
- **Decisions already made:**
  - A real `Status` column, not a soft-delete — `Attendance`/`Submission` history for an ended session stays fully intact and queryable.
  - No idle-timeout auto-end — a session only ends when the teacher explicitly ends it. See [CONTRACT.md → Open decisions](CONTRACT.md#open-decisions).
- **Done when:** clicking "End session" on the teacher dashboard immediately
  redirects every connected student to the entry screen, and the session's
  code stops being offered by `today-latest` or accepted by a fresh manual
  join.

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
  - "passed" is per-(student, assignment) EXISTS a passing submission, **not** a row average; `project` assignments (`passed = null`) are excluded from pass lists. See [CONTRACT.md](CONTRACT.md#teacher-dashboard-hydration-attendance--progress).
- **Open questions:**
  - [ ] Live per-assignment progress push (`ProgressUpdated`) so the dashboard updates without re-fetching — still backlog.
- **Done when:** a teacher who reloads mid-class sees the full roster of everyone who joined **and** each student's passed assignments, not just who's currently connected.

---

## S11 — Teacher follow: point students at the assignment I'm on

**As a** teacher, **I want** to signal which assignment I'm currently discussing,
**so that** students in the room can jump there without me reading out ids or
titles.

- **Transport:** SignalR only (hub method trigger + room broadcast — no REST leg,
  unlike the timer, since the teacher already holds a live hub connection)
- **Contract:** [`FocusAssignment`](CONTRACT.md#follow-teacher--room-broadcast) → `AssignmentFocused` event
- **Frontend:** ✅ built — a `Focus` button on each assignment in the teacher's
  `AssignmentSetPreview` calls `sessionHub.focusAssignment(code, id)`; a `Live`
  badge marks the currently-focused one. Students receive `AssignmentFocused` in
  `useStudentSession` and, if it names an assignment other than the one they're
  looking at, `StudentIde` shows a `TeacherFollowBanner` ("teacher is on _X_ —
  Follow →"); clicking it navigates via the existing assignment-select path. If
  the student is already on the focused assignment, the Toolbar shows a small
  "following teacher" pill instead of a banner.
- **Backend:** ✅ built — `SessionHub.FocusAssignment` stores the id on
  `SessionStore` (so a late joiner's `JoinSession` reply includes
  `focusedAssignmentId`, mirroring the timer's `activeTimer`) and broadcasts
  `AssignmentFocused` to Group `code`.
- **Depends on:** [Sessions / rooms](CONTRACT.md#sessions-rooms) — scoped to a
  room, so solo students never see it.
- **Decisions already made:**
  - Non-coercive, like the timer — a banner/badge invitation, never a forced
    navigation.
  - The room stores only the *latest* focused assignment (a single
    `int?`, overwritten each call) — no history of what was focused when.
- **Done when:** a teacher clicking Focus on an assignment shows a Follow banner
  on every other student's screen for that assignment, clicking it takes them
  there, and a student who joins after the fact still sees the current focus.

---

## Backlog (unwritten)

Stub stories — flesh out before building.

- Teacher sees live student progress via a `ProgressUpdated` broadcast (the live-delta half of S10 — hydration is defined; the push is not).
- Student picks an assignment from the sidebar.
- ~~Teacher manually marks a `project` submission as passed/failed~~ — dropped, not deferred: `project` no longer submits at all (see [CONTRACT.md](CONTRACT.md#mini-projects-are-vs-code-only)), so there's nothing to review in-app.
- Multi-file execution in `PistonClient`/`ExecutorService` — needed to actually run the Day-3 single-class assignments (`person-class`/`flight-ticket-class`/`container-class`) via `execute`'s `harness`; see [CONTRACT.md](CONTRACT.md#post-apiexecute) and [SCHEMA.md](SCHEMA.md#grading-rules-are-data-evaluated-by-one-backend-engine). This is an implementation gap, not a contract gap — `files`/`entryClass` are already documented.
