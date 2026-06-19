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

## S2 — Submit a finished task

**As a** student, **I want** to submit my code when I've finished a task,
**so that** my progress is recorded.

- **Transport:** REST
- **Contract:** `POST /api/submission` *(not yet defined — see Open decisions)*
- **Introduces:** identity (`studentId`) + persisted progress. Built on top of `execute`.
- **Open questions:**
  - [ ] Payload — `{ studentId, taskId, code }` → response shape?
  - [ ] How is "completed" decided — student self-marks, or output checked against expected?
  - [ ] Where is progress stored?
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

## Backlog (unwritten)

Stub stories — flesh out before building.

- Teacher sees live student progress (`ProgressUpdated` broadcast to the teacher view).
- Student picks a task from the sidebar / sees their progress.
- Solo student uses the site with no room (self-paced).
