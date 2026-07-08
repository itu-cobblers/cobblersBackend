# Data Model (Persistence)

This is the backend's persistence layer — how Sessions, Students, Tasks, and
Submissions are actually stored. [CONTRACT.md](CONTRACT.md) governs the
frontend↔backend wire format; this file governs what's behind it.

> **Rule:** if a decision here would change what the frontend sends or
> receives, it belongs in CONTRACT.md too — this file should never be the
> only place a frontend-visible behavior is documented.

> **Status:** design agreed, not yet implemented. No EF Core / `DbContext`
> exists in the codebase yet — this is the target schema the ongoing DB work
> is building toward. `SessionStore` (in-memory) is what it replaces.

User stories that drove these decisions live in [STORIES.md](STORIES.md).

---

## Entities

### Student
| Column | Type | Notes |
|---|---|---|
| `Id` | string (PK) | Client-generated UUID (see CONTRACT.md Identity). Not auto-incremented — the client is the source of the value. |
| `DisplayName` | string | Set on first join; a label, not auth. |

### Session
| Column | Type | Notes |
|---|---|---|
| `SessionId` | string PK | Server-generated UUID |
| `Code` | string | The 4-char join code shown to students. |
| `Year` | int | Set at creation time (`DateTimeOffset.UtcNow.Year`). See [Design decisions](#code-uniqueness-is-scoped-by-year). |
| `TasksetId` | FK → TaskSet | Which content this session's day uses. |
| `CreateDatetime` | datetime | |

Constraint: **`UNIQUE (Code, Year)`** 

### Attendance
| Column | Type | Notes |
|---|---|---|
| `StudentId` | FK → Student | ┐ |
| `SessionId` | FK → Session | ┘ composite PK |
| `JoinedAt` | datetime | |

One row per (student, session) pair. A `JoinSession` call is what creates a
`Student` (if new) and this row (see CONTRACT.md Sessions).

### TaskSet
| Column | Type | Notes |
|---|---|---|
| `TasksetId` | PK | |
| `DisplayTitle` | string | Human-readable name (e.g. "BootIT Day 1 — 2026"), for the teacher's session-creation picker. See [Design decisions](#taskset-gets-a-human-readable-displaytitle). |

A named collection of tasks, referenced by `Session.TasksetId`. Reused across
years by pointing multiple `Session` rows at the same `TasksetId` — content
does not fork per year unless someone deliberately authors a new `TaskSet`.

### TaskSetTask
| Column | Type | Notes |
|---|---|---|
| `Id` | surrogate PK (auto-increment) | Exists only to order rows by creation — ascending `Id` = the order a task was added to the set. Not exposed to the frontend. |
| `TasksetId` | FK → TaskSet | |
| `TaskId` | FK → Task | |

Constraint: `UNIQUE (TasksetId, TaskId)` — a task can't be added to the same
set twice.

No `Position` column. See [Design decisions](#tasksettask-drops-position) for
why, and for a flagged assumption about what "ordering" means here.

A real join table, not an id-list column on `TaskSet` — gives FK integrity
(can't reference a deleted/nonexistent task) that a JSON/array column
wouldn't.

### Task
| Column | Type | Notes |
|---|---|---|
| `Id` | PK (fresh identity) | **Not** the frontend's current 0–34 numbering — see [Design decisions](#taskid-is-a-fresh-identity). |
| `Kind` | enum: `Code` \| `Predict` \| `Project` | |
| `Title` | string | |
| `Description` | string | |
| `Hint` | string? | |
| `ContentJson` | json | Kind-specific payload, always safe to send to the student. Shape per kind below. |
| `SampleSolutionJson` | json? | Kind-specific reference solution. **Not** part of `ContentJson` — see [Design decisions](#sample-solution-is-a-separate-column). |

`day` and `difficulty` (present in the frontend's current `TaskBase`) are
**dropped**. `day` is expressed by `TaskSetTask` membership instead.

`ContentJson` shape by kind (mirrors the frontend's `CodeTask` / `PredictTask`
/ `ProjectTask`, minus `check`):

```
Code:    { starter?, stdin?, harness?: { files: [{name, content}], entryClass }, solutionFile? }
Predict: { snippet, expectedOutput, accept?: string[] }
Project: { brief, requiredClasses?: string[], entryClass? }
```

`SampleSolutionJson` shape by kind:

```
Code:    string                    // one Java source file, same shape as `starter`
Project: [{ name, content }]       // reference files, same shape as `harness.files`
Predict: not used — ContentJson.expectedOutput already is the answer
```

### Submission
| Column | Type | Notes |
|---|---|---|
| `SubId` | PK (surrogate) | Not `(StudentId, TaskId)` — a student can submit the same task multiple times, including failing attempts. |
| `StudentId` | FK → Student | |
| `TaskId` | FK → Task | |
| `SessionId` | FK → Session, **nullable** | Null for solo/practice submissions made without ever joining a room. See [Design decisions](#sessionid-is-nullable-on-submission). |
| `ContentJson` | json | Full submitted payload — a string for Code/Predict, `SourceFile[]` for Project. |
| `ResultJson` | json? | Full raw execution result (`stdout`/`stderr`/`exitCode`) for Code/Project. Null for Predict (no execution happens). |
| `Passed` | bool? | Server-computed verdict (see [Design decisions](#grading-lives-in-backend-code-not-the-database)). Null = not automatically gradable (e.g. Project today). |
| `SubmittedAt` | datetime | Not nullable — was missing from the original sketch; needed to order history and to tell submissions across different sessions/years apart. |

---

## Design decisions

### `SessionId` is nullable on `Submission`
Two populations submit work: students who joined a teacher's room (`code`),
and solo students working from a hardcoded `tasksetId` the frontend already
knows, who never call `JoinSession` and so never get an `Attendance` row.
Rather than model these as two flows, `Submission.SessionId` is just optional
— one endpoint, one history table, for both. No new entity was needed.

### Task content fetch doesn't require a session
`GET /api/tasksets/{tasksetId}/tasks` (see CONTRACT.md) takes a `tasksetId`
directly rather than being nested under `/sessions/{code}`. The solo cohort
calls it with their hardcoded id; the room cohort resolves `tasksetId` once
from `GET /api/sessions/{code}` and then calls the same endpoint. One task-list
endpoint serves both, instead of two paths returning the same shape.

### Sample solution is a separate column
`SampleSolutionJson` is not folded into `ContentJson` because `ContentJson` is
sent to the student the moment they open a task — bundling the answer in
there would leak it in the network tab before the student attempts anything.
Keeping it a separate field makes "don't send this yet" an API-layer decision
(simply omit the field from the response) rather than something that has to
be filtered out of a shared blob.

### Sample solution reveal uses one rule for both solo and classroom
Two options were on the table for when a *classroom* student (as opposed to
solo) can see a task's sample solution:

- **A. Teacher-set delay** — the teacher configures a timeout; the solution
  stays hidden until it elapses, discouraging students from peeking after one
  failed attempt.
- **B. Same rule as solo** — reveal as soon as the student has submitted the
  task at least once, no timer.

**Decision: B**, for both engineering-cost and pedagogical reasons:

- Students in a room work through tasks at their own pace, not in lockstep —
  a *single* delay can't be scoped to "a session," it would have to be scoped
  to *(student, task)*, which means tracking a start time per student per
  task, teacher-facing controls to set/adjust it, and a second, divergent
  code path from solo mode. That's real, ongoing complexity for a rule whose
  main job — stop a student from seeing the answer before trying — is already
  done by the "at least one submission" gate.
- It also fits the product's existing tone better. The task copy (a hygge
  café, a blackmarket-kitchen catering game, a "just try it" grading style)
  reads as low-pressure and trust-the-student, not surveillance-and-delay.
  Gating answers behind a teacher-controlled clock is a more controlling
  mechanic than anything else in the app, for a marginal benefit over "you
  already had to try."

So: `GET /api/tasks/{taskId}/solution?studentId=...` is available whenever
any `Submission` exists for that `(studentId, taskId)` pair — solo or in a
room, no session-specific logic. See [CONTRACT.md](CONTRACT.md#solution).

### Grading lives in backend code, not the database
`CodeTask.check()` is a function in the frontend today — functions aren't
data and can't be stored in a DB column. Rather than inventing a serializable
rule format for it, grading logic for `Code` tasks is ported to backend code:
a lookup keyed by `TaskId` (e.g. `Dictionary<int, Func<CheckResult, Verdict>>`),
evaluated server-side after the Piston result comes back. This makes
`Submission.Passed` authoritative — the client no longer self-reports whether
it passed.

`Predict` tasks don't need a per-task entry in that lookup: their grading is
one generic algorithm (normalize + compare against `ContentJson.expectedOutput`
/ `accept[]`), driven entirely by data already in `Task`.

`Project` tasks have no automated check today (same as the frontend currently
— they're manually reviewed). `Submission.Passed` stays `null` for them; this
is an existing gap, not a regression introduced by this design.

> Known follow-on work: running a `Project` submission at all requires
> `PistonClient` to support multi-file execution — it currently hardcodes a
> single `Main.java` (see [CLAUDE.md](CLAUDE.md), "Java-only, single-class
> assumption"). Automated grading for `Project` submissions is unblocked by,
> but separate from, that change.

### `Code` uniqueness is scoped by year
There is no course/cohort concept — only a year. `Session.Code` was unique
only among *currently active* in-memory rooms (see CONTRACT.md); once
sessions persist indefinitely, that scope has to be explicit. `UNIQUE (Code,
Year)` means the same 4-char code can be reissued in a later year without a
collision, without introducing an entity for something that doesn't exist in
practice.

### `TaskId` is a fresh identity
The frontend's current `id` (0–34) doubles as a `localStorage` key for
tracking which tasks are done. Once `Submission` persists server-side,
"has this student completed this task" is answered by querying for a passing
`Submission`, not by a client-side id list — so there's no reason to preserve
the old numbering, and `Task.Id` starts fresh once content moves into the DB.
This retires the frontend's local completion-tracking hack; it does not need
to be reproduced.

### `TaskSet` gets a human-readable `DisplayTitle`
Resolves a previously open question. The teacher's session-creation flow
(picking which `TaskSet` to run today — see [CONTRACT.md](CONTRACT.md#tasks),
[STORIES.md](STORIES.md) S6) needs something better than a raw id in a
dropdown. `DisplayTitle` is authored alongside the content, not derived.

### `TaskSetTask` drops `Position`
Rather than maintain an explicit ordering integer (renumbering on
insert/reorder), `TaskSetTask` relies on its own surrogate `Id` — ascending
`Id` is insertion order, so a set's tasks come back in the order they were
authored, with no bookkeeping.

> **Flagged assumption:** "newest at top" from the request driving this
> change is read here as describing the **`TaskSet` picker** (a teacher
> choosing *which set* to use — most recently authored one is the one
> they're probably looking for), not as reversing the order of *tasks within
> a set*. Reversing task order within a set would scramble the intentional
> Day-1-basics → Day-3-classes progression, which is presumably still wanted
> for the student-facing task sequence. If "newest task first, within a set"
> was actually intended, say so and this flips to `ORDER BY Id DESC` for
> `TaskSetTask` specifically.

### Welcome-back resume suggestion needs no new schema (Frontend only)
On login, a student who joined a session yesterday can be prompted to
continue in today's session, without retyping a code. This needs no new
columns — `Session.CreateDatetime` and `Attendance` already carry what's
needed. See [CONTRACT.md](CONTRACT.md#resume-suggestion-planned) for the
full plan (endpoint shape, matching heuristic, edge cases). Noted here only
so it's clear this is orchestration/query logic, not a schema change —
deliberately not introducing a course/cohort entity just to answer "is this
the same class as yesterday" (see [STORIES.md](STORIES.md) S9).

### Persistence replaces `SessionStore`'s ephemeral-by-design contract 
`SessionStore` (in-memory) is explicitly ephemeral: a server restart loses
all rooms. That contract no longer holds once `Session` / `Attendance` /
`Student` move into the DB — that's the point of this document. The live
SignalR roster (who's currently connected) stays in-memory and *is* still
ephemeral; it's a separate, smaller concern from the persisted historical
record of who attended.

---

## Open decisions

- [ ] How does a `Project` submission ever get `Passed = true` — manual teacher review needs an endpoint/UI, which doesn't exist yet.
- [ ] Migration of the 35 existing frontend tasks into `Task` rows — one-time script, not covered by this document.
- [ ] Resume-suggestion tie-break: if more than one `Session` was created "today," which one is suggested — most recent
      `CreateDatetime`? (Single-class-at-a-time assumption makes this unlikely in practice, but not impossible.)
- [ ] Resume-suggestion + `Year` boundary: does the prompt make sense across a year rollover (student's last `Attendance` was
      December of last year)? Probably rare enough to ignore, flagging in case it isn't.
