# Data Model (Persistence)

This is the backend's persistence layer — how Sessions, Students, Tasks, and
Submissions are actually stored. [CONTRACT.md](CONTRACT.md) governs the
frontend↔backend wire format; this file governs what's behind it.

> **Rule:** if a decision here would change what the frontend sends or
> receives, it belongs in CONTRACT.md too — this file should never be the
> only place a frontend-visible behavior is documented.

> **Status:** implemented as EF Core entities + configurations under
> `cobblersBackend/Data/`, with a single `InitialCreate` migration. Session
> creation still runs through `SessionStore` (in-memory); the DB-backed write
> paths (persisting `Session` / `Attendance` / `Submission`) are not wired to
> controllers yet.

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
| `SessionId` | string PK | App-generated in C# (`Guid`). `ValueGeneratedNever`. |
| `Code` | string | The 4-char join code shown to students. App-generated (random). |
| `TasksetId` | FK → TaskSet | Which content this session's day uses. |
| `CreateAt` | datetime | **DB-owned** — stamped `now()` on insert, never sent by a caller. See [Value generation](#value-generation--who-owns-each-column). |

Constraint: **`UNIQUE (Code)`** — see [Design decisions](#code-uniqueness-is-global).

### Attendance
| Column | Type | Notes |
|---|---|---|
| `StudentId` | FK → Student | ┐ |
| `SessionId` | FK → Session | ┘ composite PK |
| `JoinedAt` | datetime | **DB-owned** — stamped `now()` on insert. See [Value generation](#value-generation--who-owns-each-column). |

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
| `Id` | surrogate PK (auto-increment) | DB identity (`ValueGeneratedOnAdd`). Internal surrogate, not exposed to the frontend. |
| `TasksetId` | FK → TaskSet | |
| `TaskId` | FK → Task | |
| `OrderIndex` | int | 0-based position of the task within the set — maps to the frontend's array index. Caller-provided. See [Design decisions](#tasksettask-carries-an-explicit-orderindex). |

Constraints:
- `UNIQUE (TasksetId, TaskId)` — a task can't be added to the same set twice.
- `UNIQUE (TasksetId, OrderIndex)` — two tasks can't share a position in the same set.

A real join table, not an id-list column on `TaskSet` — gives FK integrity
(can't reference a deleted/nonexistent task) that a JSON/array column
wouldn't.

### Task

> **CLR name:** the entity class is **`Assignment`** (renamed 2026-07-16 to stop
> colliding with `System.Threading.Tasks.Task`). Everything persistence- and
> wire-facing keeps the "task" name: table `task`, columns `task_id`, wire term
> `taskId`, and the FK constraint names (pinned via `HasConstraintName` so the
> rename stayed C#-only — see `AssignmentConfiguration`). `TaskSet`/`TaskSetTask`
> deliberately not cascaded.

| Column | Type | Notes |
|---|---|---|
| `Id` | PK (fresh identity) | **Not** the frontend's current 0–34 numbering — see [Design decisions](#taskid-is-a-fresh-identity). |
| `Slug` | string, UNIQUE | Stable natural key (kebab-case, e.g. `hello-world`). Identical across databases while `Id` is DB-assigned — the seed script upserts on it, and any per-task code hook keys on it. **Internal only**, never exposed on the API. |
| `Kind` | enum: `Code` \| `Predict` \| `Project` | |
| `Title` | string | |
| `Description` | string | |
| `Hint` | string? | |
| `ContentJson` | json | Kind-specific payload, always safe to send to the student. Shape per kind below. |
| `SampleSolutionJson` | json? | Kind-specific reference solution. **Not** part of `ContentJson` — see [Design decisions](#sample-solution-is-a-separate-column). |
| `GradingJson` | json? | Serializable grading rules for `Code` tasks — see [Design decisions](#grading-rules-are-data-evaluated-by-one-backend-engine). `null` = not auto-gradable (`Project`, NIM) or graded generically (`Predict`). Never sent to the client, same as `SampleSolutionJson`. |

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
| `SubId` | Guid PK (surrogate) | App-generated in C# (`Guid.NewGuid()`), `ValueGeneratedNever`. Not `(StudentId, TaskId)` — a student can submit the same task multiple times, including failing attempts. |
| `StudentId` | FK → Student | |
| `TaskId` | FK → Task | |
| `SessionId` | FK → Session, **nullable** | Null for solo/practice submissions made without ever joining a room. See [Design decisions](#sessionid-is-nullable-on-submission). |
| `ContentJson` | json | Full submitted payload — a string for Code/Predict, `SourceFile[]` for Project. |
| `ResultJson` | json? | Full raw execution result (`stdout`/`stderr`/`exitCode`) for Code/Project. Null for Predict (no execution happens). |
| `Passed` | bool? | Server-computed verdict (see [Design decisions](#grading-rules-are-data-evaluated-by-one-backend-engine)). Null = not automatically gradable (e.g. Project today). |
| `SubmittedAt` | datetime | **DB-owned** — stamped `now()` on insert, not nullable. Needed to order history and to tell submissions apart. See [Value generation](#value-generation--who-owns-each-column). |

---

## Design decisions

### Value generation — who owns each column
Every column's value comes from exactly one of three places. Which one it is
fixes both the entity (`required` or not) and the EF configuration:

| Category | Who produces it | Entity | Configuration | Examples |
|---|---|---|---|---|
| **A. Provided** | The caller — client input, a FK reference, or seed data | `required` | `ValueGeneratedNever`, no default | `Student.Id` (client), every FK, `TaskSet.TasksetId`, `TaskSetTask.OrderIndex` |
| **B. DB-generated** | Postgres, on insert | **not** `required` | `ValueGeneratedOnAdd` (int identity) *or* `HasDefaultValueSql` (uuid / time) | `Task.Id`, `TaskSetTask.Id`, all timestamps |
| **C. App-generated** | C# at runtime | `required` | `ValueGeneratedNever`, no default | `Session.SessionId`, `Session.Code`, `Submission.SubId` |

Two consequences worth stating outright:

- **Timestamps are DB-owned (category B).** `Session.CreateAt`,
  `Attendance.JoinedAt`, and `Submission.SubmittedAt` are stamped by the
  database (`DEFAULT now()`); no request DTO carries them and C# code must not
  set them. This makes them un-spoofable and gives every row a single clock.
  That the request shape never includes a timestamp is a CONTRACT.md-relevant
  fact.
- **`required` is not a generator.** It only forces the C# object initializer
  to set a value — it never produces one. So it belongs on A and C (someone
  has to hand a value in) but never on B (the DB fills it; requiring it would
  force callers to invent a value, defeating the point). In particular an
  `int` identity PK is never `required`.

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

### Grading rules are data, evaluated by one backend engine
> **Revises an earlier decision.** The first version of this section ported
> each `CodeTask.check()` to backend code as a lookup keyed by `TaskId`
> (`Dictionary<int, Func<CheckResult, Verdict>>`). That broke once `Task.Id`
> became purely DB-assigned (ids can differ between a local DB and the VM DB,
> so C# has no stable key), and it meant maintaining task content (SQL) and
> grading logic (C#) in two places that could drift.

Instead, grading rules are **data stored with the task** (`Task.GradingJson`,
jsonb) and the backend has **one generic evaluator** (`ITaskGrader` /
`TaskGrader` in `Services/`), run server-side after the Piston result comes
back. This makes `Submission.Passed` authoritative — the client no longer
self-reports whether it passed — and adding or re-tuning a task touches only
the seed SQL, no C# deploy.

A rule node is one of:

```jsonc
{ "all": [ <node>, ... ] }                                      // AND
{ "any": [ <node>, ... ] }                                      // OR — e.g. accept "Hello World!" or "Hello, World!"
{ "not": <node> }                                               // e.g. FlightTicket: price must never go negative
{ "target": "stdout"|"code", "op": "contains",     "value": "2024" }
{ "target": "stdout",        "op": "containsLine", "value": "50" }        // trimmed-line match
{ "target": "stdout"|"code", "op": "regex",        "pattern": "c2f\\s*\\(", "flags": "i" }
{ "op": "nonEmptyStdout" }                                      // café task: any output passes
{ "op": "custom", "key": "<slug>" }                             // escape hatch — see below
```

Grading only runs on a successful execution — a non-zero exit code fails
before any rule is evaluated. All 35 current frontend `check()` functions
decompose into these primitives (verified against the frontend's `tasks.ts` +
`lib/grade.ts`); the frontend's `signals` side-channel (the café-name display)
stays a client-side nicety derived from stdout — the server verdict is just
`passed`.

- `Predict` tasks don't use `GradingJson`: their grading is one generic
  algorithm (normalize + compare against `ContentJson.expectedOutput` /
  `accept[]`), driven entirely by data already in `Task`.
- `custom` is the escape hatch if a future task outgrows the DSL: it resolves
  a handler from a small C# registry keyed by **`Slug`** (stable across
  databases, unlike `Id`). No current task needs it — prefer extending the
  DSL with a new op over reaching for `custom`.

`Project` tasks have no automated check today (same as the frontend currently
— they're manually reviewed). `Submission.Passed` stays `null` for them; this
is an existing gap, not a regression introduced by this design.

> Known follow-on work: running a `Project` submission at all requires
> `PistonClient` to support multi-file execution — it currently hardcodes a
> single `Main.java` (see [CLAUDE.md](CLAUDE.md), "Java-only, single-class
> assumption"). Automated grading for `Project` submissions is unblocked by,
> but separate from, that change.

### `Code` uniqueness is global
`Session.Code` was originally scoped `UNIQUE (Code, Year)` so a code could be
reissued in a later year. `Year` has since been dropped — it existed purely to
widen the uniqueness scope, for a collision that is vanishingly rare at
bootcamp scale (4 chars over a 32-symbol alphabet ≈ 1M combinations). The
constraint is now a plain **`UNIQUE (Code)`**.

Collisions are still possible (birthday paradox), so code allocation must
**insert-and-retry**: generate a code, attempt the insert, and on a unique-key
violation (Postgres `23505`) regenerate and try again — never check-then-insert
(that races). `SessionStore` already does this in-memory; the DB write path
must do the same.

> **Future option — active-only uniqueness.** To make codes *recyclable* once
> a session ends (so the code space never exhausts), add a nullable `ClosedAt`
> timestamp and make the index partial: `UNIQUE (Code) WHERE closed_at IS
> NULL`. A partial-index predicate must be immutable, so it keys off
> `closed_at IS NULL`, **not** a time comparison like `expires_at > now()`.
> Deferred — global uniqueness is enough at current scale, and this needs a
> session lifecycle (something has to mark a session closed) that doesn't
> exist yet.

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

### `TaskSetTask` carries an explicit `OrderIndex`
An earlier revision dropped a position column and relied on the surrogate `Id`
(ascending `Id` = insertion order). That's been **reversed**: task order within
a set is real, student-facing data — the intentional Day-1-basics →
Day-3-classes progression, and the frontend's array-index addressing of tasks.
So it gets its own explicit **`OrderIndex`** (0-based, matching the frontend's
array index) rather than being implied by an auto-increment key that renumbers
awkwardly on reorder. `UNIQUE (TasksetId, OrderIndex)` stops two tasks sharing
a slot.

The index only prevents *duplicate* positions — it can't enforce a gapless
`0,1,2,…` sequence. Seed/authoring code is responsible for numbering a set's
tasks contiguously from 0.

> Because `OrderIndex` is how the frontend addresses tasks within a set, it's
> a CONTRACT.md-relevant field — the task-list response order (and any
> index-based addressing) should be defined against it.

### Welcome-back resume suggestion needs no new schema (Frontend only)
On login, a student who joined a session yesterday can be prompted to
continue in today's session, without retyping a code. This needs no new
columns — `Session.CreateAt` and `Attendance` already carry what's
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
- [x] Migration of the 35 existing frontend tasks into `Task` rows — done via the idempotent
      [scripts/seed-tasks.sql](scripts/seed-tasks.sql) (upserts keyed on `Slug`; re-runnable against any environment).
- [ ] Resume-suggestion tie-break: if more than one `Session` was created "today," which one is suggested — most recent
      `CreateAt`? (Single-class-at-a-time assumption makes this unlikely in practice, but not impossible.)
- [ ] Resume-suggestion across a year rollover (student's last `Attendance` was December of last year): does the prompt still
      make sense? Probably rare enough to ignore, flagging in case it isn't. (`Year` is no longer a column — this is purely a
      calendar-date question on `CreateAt` / `JoinedAt` now.)
