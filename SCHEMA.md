# Data Model (Persistence)

This is the backend's persistence layer — how Sessions, Students, Assignments, and
Submissions are actually stored. [CONTRACT.md](CONTRACT.md) governs the
frontend↔backend wire format; this file governs what's behind it.

> **Rule:** if a decision here would change what the frontend sends or
> receives, it belongs in CONTRACT.md too — this file should never be the
> only place a frontend-visible behavior is documented.

> **Status:** implemented as EF Core entities + configurations under
> `cobblersBackend/Data/`, across several migrations. `Session`/`Attendance`
> persist via `SessionService`/`AttendanceService`; `SessionStore` (in-memory)
> now holds only the live SignalR roster + active timer, not the persisted
> record. `Submission` writes are wired up (`SubmissionController` →
> `SubmissionService`, S2). Two **reads** over the same table are still
> missing: `GET /api/students/{studentId}/submissions` (S5) and
> `GET /api/students/{studentId}/resume-suggestion` (S9) — see
> [CONTRACT.md](CONTRACT.md#submission) / [STORIES.md](STORIES.md). Both are
> pure queries; neither needs a schema change.

> **Naming:** fully renamed to **Assignment** as of 2026-07-17 — code, tables,
> columns, and the wire contract (CONTRACT.md) all agree. `Assignment` (was
> `Task`), `AssignmentSet` (was `TaskSet`), `AssignmentSetAssignment` (was
> `TaskSetTask`), `AssignmentGrader` (was `TaskGrader`). The rename landed as
> two migrations (`RenameAssignmentPhysicalNames`, `RenameTaskSetTablesToAssignmentSet`)
> that only rename tables/columns/constraints — no data loss, verified against
> a fresh Postgres via the test suite. See CLAUDE.md for the C#-side history
> (the CLR type was renamed once already, back on 2026-07-16, before this
> wider wire/DB sweep).

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
| `AssignmentSetId` | FK → AssignmentSet | Which content this session's day uses. |
| `CreateAt` | datetime | **DB-owned** — stamped `now()` on insert, never sent by a caller. See [Value generation](#value-generation--who-owns-each-column). |
| `Status` | text (enum: `active` \| `ended`) | App-owned, defaults `active` on insert. Flips to `ended` via [`POST /api/sessions/{code}/end`](CONTRACT.md#post-apisessionscodeend). See [`Session.Status`](#sessionstatus) below. |

Constraints: **`UNIQUE (Code)`** — see [Design decisions](#code-uniqueness-is-global) —
and `ck_session_status CHECK (status IN ('active', 'ended'))`.

#### `Session.Status`

A teacher-ended room needs to stay "gone" for future lookups (new joins,
[`today-latest`](CONTRACT.md#get-apisessionstoday-latest-student-entry-screen--is-a-session-live-today))
across a server restart — the in-memory `SessionStore` roster alone can't
carry that, since it's explicitly ephemeral (see
[Persistence replaces `SessionStore`'s ephemeral-by-design contract](#persistence-replaces-sessionstores-ephemeral-by-design-contract)
below). A real column, not a `DeletedAt` soft-delete, because the row and its
`Attendance`/`Submission` history stay fully intact and queryable — "ended"
is a lifecycle state, not a deletion. Modeled as a C# enum (`SessionStatus`),
persisted as lowercase text (not an int) so the DB value is self-describing
in a `psql` shell without a lookup table. `GET /api/sessions/{code}` and
`GET /api/sessions/today-latest` both filter `Status == Active`; nothing
currently reads `ended` sessions back out over the API — only their
`Attendance`/`Submission` rows remain reachable, via the student/history
endpoints, not via a session lookup.

### Attendance
| Column | Type | Notes |
|---|---|---|
| `StudentId` | FK → Student | ┐ |
| `SessionId` | FK → Session | ┘ composite PK |
| `JoinedAt` | datetime | **DB-owned** — stamped `now()` on insert. See [Value generation](#value-generation--who-owns-each-column). |

One row per (student, session) pair. A `JoinSession` call is what creates a
`Student` (if new) and this row (see CONTRACT.md Sessions).

### AssignmentSet
| Column | Type | Notes |
|---|---|---|
| `AssignmentSetId` | PK | |
| `DisplayTitle` | string | Human-readable name (e.g. "BootIT Day 1 — 2026"), for the teacher's session-creation picker. See [Design decisions](#assignmentset-gets-a-human-readable-displaytitle). |

A named collection of assignments, referenced by `Session.AssignmentSetId`. Reused across
years by pointing multiple `Session` rows at the same `AssignmentSetId` — content
does not fork per year unless someone deliberately authors a new `AssignmentSet`.

### AssignmentSetAssignment
| Column | Type | Notes |
|---|---|---|
| `Id` | surrogate PK (auto-increment) | DB identity (`ValueGeneratedOnAdd`). Internal surrogate, not exposed to the frontend. |
| `AssignmentSetId` | FK → AssignmentSet | |
| `AssignmentId` | FK → Assignment | |
| `OrderIndex` | int | 0-based position of the assignment within the set — maps to the frontend's array index. Caller-provided. See [Design decisions](#assignmentsetassignment-carries-an-explicit-orderindex). |

Constraints:
- `UNIQUE (AssignmentSetId, AssignmentId)` — an assignment can't be added to the same set twice.
- `UNIQUE (AssignmentSetId, OrderIndex)` — two assignments can't share a position in the same set.

A real join table, not an id-list column on `AssignmentSet` — gives FK integrity
(can't reference a deleted/nonexistent assignment) that a JSON/array column
wouldn't.

### Assignment

> **History:** the entity class was renamed `Task` → `Assignment` on 2026-07-16
> to stop colliding with `System.Threading.Tasks.Task` — at that point the DB
> and wire deliberately stayed "task" (table `task`, wire `taskId`), a
> CLR-only rename. That scope was **superseded on 2026-07-17**: CONTRACT.md's
> frontend now speaks Assignment vocabulary end to end
> (`/api/assignmentsets`, `assignmentId`), so the DB followed — table is now
> `assignment`, FK column `assignment_id`, check constraint `ck_assignment_kind`.

| Column | Type | Notes |
|---|---|---|
| `Id` | PK (fresh identity) | **Not** the frontend's current 0–33 numbering — see [Design decisions](#assignmentid-is-a-fresh-identity). |
| `Slug` | string, UNIQUE | Stable natural key (kebab-case, e.g. `hello-itu`). Identical across databases while `Id` is DB-assigned — the seed script upserts on it, and any per-assignment code hook keys on it. **Internal only**, never exposed on the API. |
| `Kind` | enum: `Code` \| `Predict` \| `Project` | |
| `Title` | string | |
| `Description` | string | |
| `LessonJson` | json? | Optional teaching blocks shown above the task. Shape: `[{ kind: "text", text } \| { kind: "code", code }, ...]`. Null = no lesson. Wire field `lesson` — a sibling of `hint`/`content`, **not** folded into `ContentJson` (mirrors the frontend's `AssignmentBase.lesson`). |
| `Hint` | string? | |
| `ContentJson` | json | Kind-specific payload, always safe to send to the student. Shape per kind below. |
| `SampleSolutionJson` | json? | Kind-specific reference solution. **Not** part of `ContentJson` — see [Design decisions](#sample-solution-is-a-separate-column). |
| `GradingJson` | json? | Serializable grading rules for `Code` assignments — see [Design decisions](#grading-rules-are-data-evaluated-by-one-backend-engine). `null` = not auto-gradable (`Project`, NIM) or graded generically (`Predict`). Never sent to the client, same as `SampleSolutionJson`. |

`day` and `difficulty` (present in the frontend's current `AssignmentBase`) are
**dropped**. `day` is expressed by `AssignmentSetAssignment` membership instead.

`ContentJson` shape by kind (mirrors the frontend's `CodeAssignment` / `PredictAssignment`
/ `ProjectAssignment`, minus `check`):

```
Code:    { starter?, starterFiles?: [{name, content}], entryClass?, stdin?, harness?: { files: [{name, content}], entryClass }, solutionFile? }
Predict: { snippet, expectedOutput, accept?: string[] }
Project: { brief, requiredClasses?: string[], entryClass? }   // display-only — see below
```

For multi-file `Code` assignments (e.g. `person-class`, `flight-ticket-class`, `container-class`), `ContentJson.starterFiles` provides initial templates for each file in the editor (e.g. `Main.java` + `Person.java`), and `entryClass` specifies which class contains the `main` method. Single-file assignments continue to use `starter`.

`Project.entryClass`/`requiredClasses` describe the eventual VS Code solution
shape (useful copy for the brief, e.g. "your `Main` should call a `Tree`
class") — they're **not** consumed by any endpoint today, since `Project`
never reaches `execute`/`submission` (see
[Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)).

`SampleSolutionJson` shape by kind:

```
Code:    string | [{ name, content }]     // single file string or multi-file array
Project: [{ name, content }]             // reference files, same shape as `harness.files`
Predict: not used — ContentJson.expectedOutput already is the answer
```

### Submission
| Column | Type | Notes |
|---|---|---|
| `SubId` | Guid PK (surrogate) | App-generated in C# (`Guid.NewGuid()`), `ValueGeneratedNever`. Not `(StudentId, AssignmentId)` — a student can submit the same assignment multiple times, including failing attempts. |
| `StudentId` | FK → Student | |
| `AssignmentId` | FK → Assignment | |
| `SessionId` | FK → Session, **nullable** | Null for solo/practice submissions made without ever joining a room. See [Design decisions](#sessionid-is-nullable-on-submission). |
| `ContentJson` | json | Full submitted payload — a string for single-file Code/Predict, `SourceFile[]` (`[{name, content}]`) for multi-file Code. |
| `ResultJson` | json? | Full raw execution result (`stdout`/`stderr`/`exitCode`) for Code/Project. Null for Predict (no execution happens). |
| `Passed` | bool? | Server-computed verdict (see [Design decisions](#grading-rules-are-data-evaluated-by-one-backend-engine)). Null = not automatically gradable (e.g. Project today). |
| `SubmittedAt` | datetime | **DB-owned** — stamped `now()` on insert, not nullable. Needed to order history and to tell submissions apart. See [Value generation](#value-generation--who-owns-each-column). |

> **Implementation note — `GET /api/students/{studentId}/submissions` (S5).**
> A single filtered, ordered read — no new query object needed:
> `Submission.Where(s => s.StudentId == studentId).OrderByDescending(s => s.SubmittedAt)`,
> projected to the wire DTO in [CONTRACT.md](CONTRACT.md#submission). Two
> details that aren't obvious from the entity alone:
> - The wire field `sessionId` is the room's **`Session.Code`** (e.g.
>   `"ABCD"`), not `Submission.SessionId` (the internal `Session` PK) — join
>   to `Session` (`LEFT JOIN`, since `SessionId` is nullable) and project
>   `Code`, or the frontend can't display/compare it against
>   `GET /api/sessions/{code}`'s `code`.
> - Deliberately **thin** — no `ContentJson`/`ResultJson` in the response.
>   The frontend's "My Progress" panel only needs `passed` + `submittedAt` +
>   which room; it never needs to replay past code/output. Don't widen this
>   DTO without a frontend reason — see CONTRACT.md's response shape.

---

## Design decisions

### Value generation — who owns each column
Every column's value comes from exactly one of three places. Which one it is
fixes both the entity (`required` or not) and the EF configuration:

| Category | Who produces it | Entity | Configuration | Examples |
|---|---|---|---|---|
| **A. Provided** | The caller — client input, a FK reference, or seed data | `required` | `ValueGeneratedNever`, no default | `Student.Id` (client), every FK, `AssignmentSet.AssignmentSetId`, `AssignmentSetAssignment.OrderIndex` |
| **B. DB-generated** | Postgres, on insert | **not** `required` | `ValueGeneratedOnAdd` (int identity) *or* `HasDefaultValueSql` (uuid / time) | `Assignment.Id`, `AssignmentSetAssignment.Id`, all timestamps |
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
and solo students working from a hardcoded `assignmentSetId` the frontend already
knows, who never call `JoinSession` and so never get an `Attendance` row.
Rather than model these as two flows, `Submission.SessionId` is just optional
— one endpoint, one history table, for both. No new entity was needed.

### Assignment content fetch doesn't require a session
`GET /api/assignmentsets/{assignmentSetId}/assignments` (see CONTRACT.md) takes an `assignmentSetId`
directly rather than being nested under `/sessions/{code}`. The solo cohort
calls it with their hardcoded id; the room cohort resolves `assignmentSetId` once
from `GET /api/sessions/{code}` and then calls the same endpoint. One assignment-list
endpoint serves both, instead of two paths returning the same shape.

### Sample solution is a separate column
`SampleSolutionJson` is not folded into `ContentJson` because `ContentJson` is
sent to the student the moment they open an assignment — bundling the answer in
there would leak it in the network tab before the student attempts anything.
Keeping it a separate field makes "don't send this yet" an API-layer decision
(simply omit the field from the response) rather than something that has to
be filtered out of a shared blob.

### Sample solution reveal uses one rule for both solo and classroom
Two options were on the table for when a *classroom* student (as opposed to
solo) can see an assignment's sample solution:

- **A. Teacher-set delay** — the teacher configures a timeout; the solution
  stays hidden until it elapses, discouraging students from peeking after one
  failed attempt.
- **B. Same rule as solo** — reveal as soon as the student has submitted the
  assignment at least once, no timer.

**Decision: B**, for both engineering-cost and pedagogical reasons:

- Students in a room work through assignments at their own pace, not in lockstep —
  a *single* delay can't be scoped to "a session," it would have to be scoped
  to *(student, assignment)*, which means tracking a start time per student per
  assignment, teacher-facing controls to set/adjust it, and a second, divergent
  code path from solo mode. That's real, ongoing complexity for a rule whose
  main job — stop a student from seeing the answer before trying — is already
  done by the "at least one submission" gate.
- It also fits the product's existing tone better. The assignment copy (a hygge
  café, a blackmarket-kitchen catering game, a "just try it" grading style)
  reads as low-pressure and trust-the-student, not surveillance-and-delay.
  Gating answers behind a teacher-controlled clock is a more controlling
  mechanic than anything else in the app, for a marginal benefit over "you
  already had to try."

So: `GET /api/assignments/{assignmentId}/solution?studentId=...` is available whenever
any `Submission` exists for that `(studentId, assignmentId)` pair — solo or in a
room, no session-specific logic. See [CONTRACT.md](CONTRACT.md#solution).

### Grading rules are data, evaluated by one backend engine
> **Revises an earlier decision.** The first version of this section ported
> each `CodeAssignment.check()` to backend code as a lookup keyed by `AssignmentId`
> (`Dictionary<int, Func<CheckResult, Verdict>>`). That broke once `Assignment.Id`
> became purely DB-assigned (ids can differ between a local DB and the VM DB,
> so C# has no stable key), and it meant maintaining assignment content (SQL) and
> grading logic (C#) in two places that could drift.

Instead, grading rules are **data stored with the assignment** (`Assignment.GradingJson`,
jsonb) and the backend has **one generic evaluator** (`IAssignmentGrader` /
`AssignmentGrader` in `Services/`), run server-side after the Piston result comes
back. This makes `Submission.Passed` authoritative — the client no longer
self-reports whether it passed — and adding or re-tuning an assignment touches only
the seed SQL, no C# deploy.

A rule node is one of:

```jsonc
{ "all": [ <node>, ... ] }                                      // AND
{ "any": [ <node>, ... ] }                                      // OR — e.g. accept "Hello World!" or "Hello, World!"
{ "not": <node> }                                               // e.g. FlightTicket: price must never go negative
{ "target": "stdout"|"code", "op": "contains",     "value": "2024" }
{ "target": "stdout",        "op": "containsLine", "value": "50" }        // trimmed-line match
{ "target": "stdout"|"code", "op": "regex",        "pattern": "c2f\\s*\\(", "flags": "i" }
{ "op": "nonEmptyStdout" }                                      // café assignment: any output passes
{ "op": "custom", "key": "<slug>" }                             // escape hatch — see below
```

Grading only runs on a successful execution — a non-zero exit code fails
before any rule is evaluated. The current frontend `check()` functions
decompose into these primitives (or a close approximation stored in
`scripts/seed-tasks.sql`, verified against the frontend's `assignments.ts` +
`lib/grade.ts`); the frontend's `signals` side-channel stays a client-side
nicety derived from stdout — the server verdict is just `passed`.

- `Predict` assignments use a dedicated `GradingJson` document (not the code
  rule tree):
  `{ "predict": { "compare": "normalized"|"exact", "expectedOutput": "...", "accept"?: string[] } }`.
  `ContentJson.expectedOutput` / `accept` stay on the wire for the post-submit
  reveal UI; the authoritative grade reads `GradingJson` only.
- `custom` is the escape hatch if a future assignment outgrows the DSL: it resolves
  a handler from a small C# registry keyed by **`Slug`** (stable across
  databases, unlike `Id`). No current assignment needs it — prefer extending the
  DSL with a new op over reaching for `custom`.

`Project` assignments have no automated check today, and — as of the
[mini-projects-are-VS-Code-only decision](#mini-projects-are-vs-code-only) —
never will need one through this app: nothing ever calls `execute` or
`submission` for `kind: "project"`, so `Submission.Passed` for `Project`
isn't just `null` today, it's a row that will never exist at all.

> **Multi-file execution is still needed** — `PistonClient` currently hardcodes a single `Main.java` (see [CLAUDE.md](CLAUDE.md), "Java-only, single-class assumption"), and must be updated to support sending multiple files (`{ name, content }[]`) to Piston. The Day-3 `Code`-kind multi-file assignments (`person-class`, `flight-ticket-class`, `container-class`) send multiple student-editable files directly in `execute` and `submission`.

### Mini-projects are VS-Code-only
The three Day-3 mini-projects (`build-a-tree`, `grandpas-time-machine`,
`grandmas-blackmarket-kitchen`) are excluded from `execute`/`submission`
entirely — see [CONTRACT.md](CONTRACT.md#mini-projects-are-vs-code-only) for
the full rationale and wire-level consequences. In short: at least one needs
`Scanner`/interactive `stdin`, which this app's stateless request/response
`execute` can't drive well, and since students pick **one of the three** to
work on (they're offered in the same slot, not sequentially), scoping the
Scanner-free project(s) in and the rest out would make the in-app experience
depend on which project a student happened to pick. So the whole `Kind.Project`
enum value is out of scope for `execute`/`submission`, not a per-assignment
flag — `Assignment.ContentJson` for `Project` needs no new field to express
this, and no schema change is needed to implement it.

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

### `AssignmentId` is a fresh identity
The frontend's current `id` (0–33) doubles as a `localStorage` key for
tracking which assignments are done. Once `Submission` persists server-side,
"has this student completed this assignment" is answered by querying for a passing
`Submission`, not by a client-side id list — so there's no reason to preserve
the old numbering, and `Assignment.Id` starts fresh once content moves into the DB.
This retires the frontend's local completion-tracking hack; it does not need
to be reproduced.

### `AssignmentSet` gets a human-readable `DisplayTitle`
Resolves a previously open question. The teacher's session-creation flow
(picking which `AssignmentSet` to run today — see [CONTRACT.md](CONTRACT.md#assignments),
[STORIES.md](STORIES.md) S6) needs something better than a raw id in a
dropdown. `DisplayTitle` is authored alongside the content, not derived.

### `AssignmentSetAssignment` carries an explicit `OrderIndex`
An earlier revision dropped a position column and relied on the surrogate `Id`
(ascending `Id` = insertion order). That's been **reversed**: assignment order within
a set is real, student-facing data — the intentional Day-1-basics →
Day-3-classes progression, and the frontend's array-index addressing of assignments.
So it gets its own explicit **`OrderIndex`** (0-based, matching the frontend's
array index) rather than being implied by an auto-increment key that renumbers
awkwardly on reorder. `UNIQUE (AssignmentSetId, OrderIndex)` stops two assignments sharing
a slot.

The index only prevents *duplicate* positions — it can't enforce a gapless
`0,1,2,…` sequence. Seed/authoring code is responsible for numbering a set's
assignments contiguously from 0.

> Because `OrderIndex` is how the frontend addresses assignments within a set, it's
> a CONTRACT.md-relevant field — the assignment-list response order (and any
> index-based addressing) should be defined against it.

### Welcome-back resume suggestion retired — superseded by `today-latest`
The original plan here was a *personalized* suggestion: on login, a student
who joined a session yesterday would be prompted to continue in today's
session, by diffing `Session.CreateAt` against their own `Attendance` rows
(no new columns needed for that either — it was pure query logic). That
plan, and its one-off frontend `WelcomeBackBanner` component, are both
**retired** — see [CONTRACT.md](CONTRACT.md#resume-suggestion-retired) for
the reasoning.

What shipped instead is simpler and needs no `Attendance` join at all:
[`GET /api/sessions/today-latest`](CONTRACT.md#get-apisessionstoday-latest-student-entry-screen--is-a-session-live-today)
just asks "is there an `active` `Session` created today," full stop —

```
var todayStart = DateTimeOffset.UtcNow.Date;
var latest = Session
  .Where(s => s.Status == SessionStatus.Active && s.CreateAt >= todayStart)
  .OrderByDescending(s => s.CreateAt)
  .FirstOrDefault();
```

— then projects `{ code: latest.Code, assignmentSetId: latest.AssignmentSetId }`
(reusing `GetSessionResponse`, the same shape as `GET /api/sessions/{code}`),
or `404 Not Found` if nothing matches. No per-student personalization, no
course/cohort entity, and — unlike the retired design — no `WelcomeBackBanner`;
the entry screen's join button just enables/disables itself against this
response.

### Persistence replaces `SessionStore`'s ephemeral-by-design contract 
`SessionStore` (in-memory) is explicitly ephemeral: a server restart loses
all rooms. That contract no longer holds once `Session` / `Attendance` /
`Student` move into the DB — that's the point of this document. The live
SignalR roster (who's currently connected) stays in-memory and *is* still
ephemeral; it's a separate, smaller concern from the persisted historical
record of who attended.

A manual [`POST /api/sessions/{code}/end`](CONTRACT.md#post-apisessionscodeend)
now clears a room's `SessionStore` entry (`RemoveRoom`) at the same time it
persists `Session.Status = ended` — the two representations (durable
`Status`, ephemeral roster/timer) are kept in lockstep on every state change
that matters, rather than one silently drifting from the other.

---

## Open decisions

- [x] How does a `Project` submission ever get `Passed = true`? — **resolved by scoping it out**, not by building manual review: `Project` is excluded from `execute`/`submission` altogether (see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)), so there's no `Submission` row to review in the first place. Manual review of an uploaded solution is no longer on the table unless this decision is revisited.
- [x] Migration of the 34 existing frontend assignments into `Assignment` rows — done via the idempotent
      [scripts/seed-tasks.sql](scripts/seed-tasks.sql) (upserts keyed on `Slug`; re-runnable against any environment).
- [x] `today-latest` tie-break: if more than one `active` `Session` was created "today," the most recent `CreateAt` wins —
      same heuristic the retired resume-suggestion design used (single-class-at-a-time assumption makes ties unlikely in
      practice, but not impossible). See [Welcome-back resume suggestion retired](#welcome-back-resume-suggestion-retired--superseded-by-today-latest).
- [ ] `today-latest` across a year/midnight rollover for a session running late: "today" is server-UTC-midnight-to-now on
      `CreateAt`, so a session created just before UTC midnight stops being "today's" the moment the clock ticks over, even
      mid-class. Probably rare enough to ignore for a 3-day workshop, flagging in case it isn't.
- [x] Session lifetime — a room ends only when the teacher manually ends it; see [`Session.Status`](#sessionstatus) and
      [CONTRACT.md → Open decisions](CONTRACT.md#open-decisions). No idle timeout.
