# API Contract

The agreement between the **frontend** (React + Monaco) and the **backend** (ASP.NET + SignalR).

> **Rule:** the frontend owner changes this file _first_; the backend owner builds to match it.
> As long as both sides honor what's written here, frontend and backend can be
> developed in parallel — each mocking the other against this contract.

> **Naming:** the entity is **Assignment** everywhere — code on both sides, and
> this wire contract (`/api/assignmentsets`, `assignmentId`, `assignmentSetId`).
> It was previously called _Task_/_taskset_; the rename happened because the
> backend entity clashed with `System.Threading.Tasks.Task`. The frontend
> (branch `feat/taskAPI`) already calls the new routes; backend routes/DTOs and
> the seed data id still carry the old naming and need to be renamed to match.

---

## Design decision: `execute` vs `submission`

There are **two separate concerns**, and they get **two separate endpoints**:

- **`execute`** — "What does this code do?" Stateless: code in, output out.
  Knows nothing about students or assignments. Called constantly (every "Run" click).
- **`submission`** — "Did this student complete this assignment?" Stateful: tied to a
  student + assignment + progress. Called once, when the student thinks they're done.
  **Built on top of `execute`** (it runs the code, then records the result).

`execute` is fully defined. `submission` is deferred until we build the
assignments/progress feature — see [Open decisions](#open-decisions).

User stories that drive these features live in [STORIES.md](STORIES.md).
Persistence/DB design for what's behind these endpoints lives in [SCHEMA.md](SCHEMA.md).

---

## Identity (no registration)

Students are **anonymous but persistent**. No login, no password, no email.

- On first visit the client generates a `studentId` (UUID) and stores it in
  `localStorage`, along with a `displayName` the student types once.
- Every request / connection carries the `studentId`.
- The **server** stores progress keyed by `studentId`. localStorage holds the
  _key_; the server holds the _data_.

```
studentId:    "uuid-v4"          // durable identity (localStorage + server progress)
displayName:  "Maria"            // a label, NOT auth — shown on the teacher dashboard
role:         "student" | "teacher"
```

Tradeoff we accept: identity is **device/browser-bound**. Clearing the browser
or switching laptops loses the key. Fine for a 3-day workshop.

> `studentId` (who you are) and **session membership** (which live room you're in)
> are different things with different lifetimes — see [Sessions](#sessions-rooms).
> `execute` and `submission` only need `studentId`; only _broadcasts_ are session-scoped.

---

## Sessions (rooms)

Two populations use the site:

- **Live cohort** — students physically in the workshop, joined to the teacher's room.
- **Solo cohort** — students given the link later, working self-paced, in no room.

A **room is a SignalR Group** named by a short session `code`. Broadcasts (e.g. the
timer) go to the group, so solo students never receive them — confusion solved by
construction, not by asking people to ignore things.

```
code:   "ABCD"   // 4 uppercase chars, skip ambiguous 0/O 1/I
                 // globally unique among sessions; regenerate on collision (insert-and-retry)
                 // see SCHEMA.md "Code uniqueness is global"
```

Session, Attendance, and Student are **persisted** (see [SCHEMA.md](SCHEMA.md))
— a room and its roster survive a server restart. The only thing that's still
in-memory and ephemeral is the _live_ SignalR roster (who currently has a
connection open); the historical record of who attended does not depend on it.

### `POST /api/sessions` (teacher creates a room)

```json
// request
{ "assignmentSetId": "day1-2026" }

// → 200 OK
{ "code": "ABCD" }
```

`assignmentSetId` comes from [`GET /api/assignmentsets`](#assignments) — the teacher picks one
before creating the room. See [STORIES.md](STORIES.md) S6.

### `JoinSession` — SignalR hub method (student joins a room)

```
JoinSession({ code, studentId, displayName })
```

- Server adds the connection to Group `code`.
- Server replies to the caller with the current state, so a late joiner / reconnect
  syncs immediately:

```json
// SessionState (reply to caller only)
{ "activeTimer": { "endsAt": "2026-06-19T14:30:00Z" } }
 // activeTimer omitted if none
```

- On a successful join the server also **broadcasts** `StudentJoined` to the group
  so an observing teacher updates live (see roster below).

> **Hub path:** the client connects to **`/hub`** (proxied in dev to the backend).

### `ObserveSession` — SignalR hub method (teacher watches a room)

```
ObserveSession(code)   // → returns the current roster
```

- Server adds the teacher's connection to Group `code` as an observer.
- **Returns** the current roster to the caller (so a reconnecting teacher re-syncs):

```json
// reply to caller only
[
  { "studentId": "uuid", "displayName": "Maria" },
  { "studentId": "uuid", "displayName": "Jonas" }
]
```

### Roster events (server → teacher observers in the room)

```json
// StudentJoined — one student, sent when someone joins
{ "studentId": "uuid", "displayName": "Maria" }

// RosterUpdated — the full list (sent on changes, e.g. a leave); optional but preferred
[ { "studentId": "uuid", "displayName": "Maria" } ]
```

A `Student` is `{ studentId: string, displayName: string }`. The teacher dashboard
renders `displayName`s; `studentId` keys them so duplicates merge.

---

## Assignments

Two populations need assignment content (see [Sessions](#sessions-rooms)): the live
cohort (in a room, `code` resolves to an `assignmentSetId`) and the solo cohort (the
frontend hardcodes `assignmentSetId: "all-assignments-for-solo-2026"`). Both hit
the same endpoint — there's no session-scoped variant.

### `GET /api/assignmentsets` (teacher — list available assignment sets)

```json
// → 200 OK
[
  { "assignmentSetId": "day1-2026", "displayTitle": "BootIT Day 1 — 2026" },
  { "assignmentSetId": "day2-2026", "displayTitle": "BootIT Day 2 — 2026" }
]
```

Feeds the teacher's session-creation picker — pick an `assignmentSetId`, pass it to
[`POST /api/sessions`](#sessions-rooms). `displayTitle` is
`AssignmentSet.DisplayTitle` (see [SCHEMA.md](SCHEMA.md)).

> **Implemented on the backend under the old task naming** (along with
> `GET /api/sessions/{code}` and `GET /api/assignmentsets/{assignmentSetId}/assignments`
> below); content is loaded by `scripts/seed-tasks.sql`. Routes, DTO fields, and
> the seed data id must be renamed to match this contract. The frontend already
> calls the real endpoints (`@lib/assignmentSetApi`, branch `feat/taskAPI`) —
> no mock remains (see [STORIES.md](STORIES.md) S6). Note `POST /api/sessions`
> **requires** the `assignmentSetId` body shown above and rejects unknown ids
> with `400`.

### `GET /api/sessions/{code}` (room cohort — resolve the room's assignment set)

```json
// → 200 OK
{ "code": "ABCD", "assignmentSetId": "day1-2026" }
```

### `GET /api/assignmentsets/{assignmentSetId}/assignments` (both cohorts — fetch content)

```json
// → 200 OK
[
  {
    "id": 101,
    "kind": "code",
    "title": "Hello ITU",
    "description": "Print exactly: Hello ITU!",
    "lesson": [
      { "kind": "text", "text": "Printing a message is the most basic thing…" },
      { "kind": "code", "code": "class Hello {\n    public static void main(String[] args) {\n        System.out.println(\"Hello World!\");\n    }\n}" }
    ],
    "hint": "System.out.println(\"Hello ITU!\");",
    "content": { "starter": "public class Main {\n...\n}" }
  },
  {
    "id": 118,
    "kind": "predict",
    "title": "While Loop Quiz 1",
    "description": "Read the loop and predict exactly what it prints.",
    "content": {
      "snippet": "int i = 10;\n...",
      "expectedOutput": "10\n9\n8\n..."
    }
  }
]
```

| Field     | Type                                   | Notes                                                                                                                    |
| --------- | -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `id`      | number                                 | Server-assigned. **Not** the frontend's current 0–33 numbering — see [SCHEMA.md](SCHEMA.md#assignmentid-is-a-fresh-identity).  |
| `kind`    | `"code"` \| `"predict"` \| `"project"` |                                                                                                                          |
| `lesson`  | `({kind:"text",text}\| {kind:"code",code})[]`? | Optional teaching blocks shown above the task. Omit when absent. Sibling of `hint`/`content` — not inside `content`. See [SCHEMA.md](SCHEMA.md). |
| `content` | object                                 | Shape depends on `kind` — mirrors the frontend's `CodeAssignment` / `PredictAssignment` / `ProjectAssignment` fields, minus grading logic / `lesson` / `check`. |

> This response never includes a sample/reference solution. That's a
> deliberate omission, not an oversight — see [SCHEMA.md](SCHEMA.md#sample-solution-is-a-separate-column).
> `check()` logic also does not travel over the wire anymore — grading moved server-side (see [Submission](#submission) below).

> **Order matters.** The array comes back sorted by each assignment's position within
> the set (`AssignmentSetAssignment.OrderIndex`, 0-based) — so the array index _is_ the
> assignment's place in the set, which is how the frontend addresses assignments. `id` (a
> fresh server identity) is **not** the ordering key. See
> [SCHEMA.md](SCHEMA.md#assignmentsetassignment-carries-an-explicit-orderindex).

This replaces the frontend's hardcoded assignment bundle as the source of truth for assignment content going forward.

---

## `POST /api/execute`

Run a single Java source file and return its output. Stateless — no identity in
the payload.

### Request

```json
{
  "code": "public class Main {\n  public static void main(String[] args) {\n    System.out.println(\"Hello World!\");\n  }\n}"
}
```

| Field        | Type                 | Notes                                                                                                                                                                                  |
| ------------ | -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `code`       | string?              | Single-file sugar: the full contents of one `Main.java`. Use this for the common case (most Day 1–2 exercises).                                                                        |
| `files`      | `{name, content}[]`? | Multi-file run. Each item is one source file. Used for Day-3 class assignments (student class + a hidden grader `Main`) and the Day-3 mini-projects (student uploads several `.java` files). |
| `entryClass` | string?              | When `files` is given, the class whose `main` to run (e.g. `"Main"`).                                                                                                                  |
| `stdin`      | string?              | Standard input piped to the program — for interactive programs (e.g. the guess-the-number game). Omit/`""` when none.                                                                  |

> **`code` XOR `files`.** Send `code` for one file, or `files` + `entryClass` for several — not both. `code: X` is equivalent to `files: [{name:"Main.java", content:X}], entryClass:"Main"`.
>
> Language is **implicit** — the backend is Java-only for now (it hardcodes `java`).
> If more languages are ever needed, add a `language` field rather than reusing `code`;
> that's a deliberate future change, not a silent one.

The **response** shape is unchanged (`status` / `stdout` / `stderr`) regardless of single- or multi-file input. The executor only compiles + runs — it never grades. Grading happens server-side via [Submission](#submission).

### Response — `200 OK`

```json
{
  "status": "success",
  "stdout": "Hello World!\n",
  "stderr": ""
}
```

| Field    | Type                                                  | Notes                                                          |
| -------- | ----------------------------------------------------- | -------------------------------------------------------------- |
| `status` | `"success"` \| `"compile_error"` \| `"runtime_error"` | Tells the frontend how to render (green output vs. red error). |
| `stdout` | string                                                | Program output. Always present (`""` if none).                 |
| `stderr` | string                                                | Error text. Always present (`""` if none).                     |

### Worked examples (from the camp slides)

**Success** — `System.out.println(42);`

```json
{ "status": "success", "stdout": "42\n", "stderr": "" }
```

**Compile error** — missing semicolon

```json
{
  "status": "compile_error",
  "stdout": "",
  "stderr": "Main.java:3: error: ';' expected"
}
```

**Runtime error** — e.g. divide by zero

```json
{
  "status": "runtime_error",
  "stdout": "",
  "stderr": "Exception in thread \"main\" java.lang.ArithmeticException: / by zero"
}
```

### Important: HTTP status vs. `status` field

- A student writing broken code is **normal and expected** → still `200 OK`,
  with `status: "compile_error"` or `"runtime_error"`.
- Non-2xx is reserved for **infrastructure** problems only: malformed request
  (`400`), executor container unreachable (`502`/`503`).
- So the frontend renders off the `status` **field**, not the HTTP code.

---

## Timer (teacher → room broadcast)

The teacher's _trigger_ is plain REST (a normal request). SignalR is used only for
the _fan-out_ to students. So only students need a live connection; the teacher side
stays simple and testable.

### `POST /api/sessions/{code}/timer` (teacher starts a timer)

```json
// request
{ "durationMinutes": 10 }

// → 200 OK  — server computes the absolute end time, stores it on the session,
//             then broadcasts TimerStarted to Group {code}
{ "endsAt": "2026-06-19T14:30:00Z" }
```

### `TimerStarted` — SignalR event (server → students in the room)

```json
{ "endsAt": "2026-06-19T14:30:00Z" }
```

Why **absolute `endsAt`**, not a duration: a student who reconnects or joins
mid-countdown shows the correct remaining time automatically (no fresh 10 minutes).
The timer is a **non-coercive reminder** — nothing is forced if it elapses.

---

## Submission

"Did this student complete this assignment?" One endpoint for all three assignment kinds,
and for both the room cohort and the solo cohort (`sessionId` is optional —
see [SCHEMA.md](SCHEMA.md#sessionid-is-nullable-on-submission)). Built on top
of `execute` for `code`/`project`; `predict` never touches the executor.

Grading is **server-side now**, not client-reported — see
[SCHEMA.md](SCHEMA.md#grading-rules-are-data-evaluated-by-one-backend-engine). The
frontend's `check()` no longer decides `passed`.

### `POST /api/assignments/{assignmentId}/submissions`

```json
// request — code / project
{ "studentId": "uuid", "sessionId": "ABCD", "content": "public class Main {...}" }

// request — predict
{ "studentId": "uuid", "sessionId": "ABCD", "content": "10\n9\n8\n..." }

// request — solo/practice (no room joined)
{ "studentId": "uuid", "content": "public class Main {...}" }
```

| Field       | Type                          | Notes                                                                                   |
| ----------- | ----------------------------- | --------------------------------------------------------------------------------------- |
| `studentId` | string                        | Required.                                                                               |
| `sessionId` | string?                       | Omit for solo/practice submissions made without joining a room.                         |
| `content`   | string \| `{name, content}[]` | A string for `code`/`predict`; a file list for `project` (matches `execute`'s `files`). |

> **`submittedAt` is server-owned** — the client never sends it. The database
> stamps it on insert and it comes back in the response only. This holds for
> every timestamp in this contract (`createAt`, `joinedAt` too): timestamps are
> DB-generated, never request input. See
> [SCHEMA.md](SCHEMA.md#value-generation--who-owns-each-column).

### Response — `200 OK`

```json
{
  "subId": "uuid",
  "passed": true,
  "result": { "status": "success", "stdout": "Hello World!\n", "stderr": "" },
  "submittedAt": "2026-06-19T14:30:00Z"
}
```

| Field    | Type     | Notes                                                                                                          |
| -------- | -------- | -------------------------------------------------------------------------------------------------------------- |
| `passed` | boolean? | Server-computed. `null` for `project` today (no automated grader yet) or any assignment without one.                 |
| `result` | object?  | Present for `code`/`project` (same shape as `execute`'s response). `null` for `predict` — nothing is executed. |

Submission history — used for the resume flow (a student returning across the
3 days, in or out of a room) and for reviewing a solo student's practice:

### `GET /api/students/{studentId}/submissions`

```json
// → 200 OK
[
  {
    "subId": "uuid",
    "assignmentId": 101,
    "sessionId": "ABCD",
    "passed": true,
    "submittedAt": "2026-06-19T14:30:00Z"
  }
]
```

---

## Solution

Reveal an assignment's sample/reference solution. **One rule for both solo and
classroom students** — see [SCHEMA.md](SCHEMA.md#sample-solution-reveal-uses-one-rule-for-both-solo-and-classroom) for why a teacher-controlled delay was considered and rejected.

### `GET /api/assignments/{assignmentId}/solution?studentId={studentId}`

```json
// → 200 OK — at least one Submission exists for (studentId, assignmentId)
{ "available": true, "solution": "public class Main {...}" }

// → 200 OK — no Submission yet
{ "available": false, "solution": null }
```

| Field       | Type                                     | Notes                                                                                                                                             |
| ----------- | ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `available` | boolean                                   | `true` once the student has submitted this assignment at least once — pass or fail, in a room or solo.                                                     |
| `solution`  | string \| `{name, content}[]` \| null     | Present only when `available`. Shape matches `Assignment.SampleSolutionJson` for the assignment's `kind`. Not applicable to `predict` (its `expectedOutput`, from Assignments, already is the answer). |

> **Not yet implemented.** Formalizes the previously-vague "reveal a sample
> solution" backlog stub now that the gating rule is decided — see
> [STORIES.md](STORIES.md) S8. Frontend: disable the "Show solution" button
> until the student has submitted at least once, with a hover explaining why.

---

## Resume suggestion (planned)

**Not yet built.** This section is a design plan, written down so frontend
and backend agree on the approach before either side builds it — see
[STORIES.md](STORIES.md) S9.

Goal: a student who attended yesterday's session, and returns today while the
teacher has already opened a new one, gets prompted to continue in today's
session instead of manually re-entering a code.

### `GET /api/students/{studentId}/resume-suggestion`

```json
// → 200 OK — a newer session exists that this student hasn't joined
{
  "suggested": {
    "code": "WXYZ",
    "assignmentSetDisplayTitle": "BootIT Day 2 — 2026",
    "createAt": "2026-06-20T08:00:00Z"
  }
}

// → 200 OK — nothing to suggest
{ "suggested": null }
```

Matching heuristic (see
[SCHEMA.md](SCHEMA.md#welcome-back-resume-suggestion-needs-no-new-schema)):
the most recently created `Session` that this `studentId` does not already
have an `Attendance` row for. No course/cohort concept — the app assumes one
active class at a time, so "the newest session" is "today's session."

**Frontend flow:** on load, if `suggested` is non-null, show "Welcome back,
{displayName}! Continue in today's session {code}?" with a one-click join
(calls the existing `JoinSession` hub method with that `code`) or a dismiss
that falls through to the normal join bar / Solo Practice choice.

| Open item                                            | |
| ----------------------------------------------------- | --- |
| Multiple sessions created the same day                | Most recent `CreateAt` wins — see [SCHEMA.md → Open decisions](SCHEMA.md#open-decisions). |
| Prompt UI / component                                 | Not designed yet — this defines the contract, not the component. |

---

## Open decisions

Resolve each _in this file_ before the relevant feature is built.

- [x] **`POST /api/submission`** — see [Submission](#submission). Payload,
      grading ownership, and persistence are decided; schema detail in
      [SCHEMA.md](SCHEMA.md).
- [x] **Assignments** — see [Assignments](#assignments). `GET /api/assignmentsets/{assignmentSetId}/assignments`
      replaces the frontend's static bundle.
- [x] **SignalR hub path** — `/hub` (see Sessions).
- [x] **Roster → teacher** — `ObserveSession` + `StudentJoined` / `RosterUpdated`
      (see Sessions). A richer `ProgressUpdated` (per-assignment progress, not just names)
      is still open.
- [x] **Progress persistence** — `Submission` rows, keyed by `studentId` (see
      [SCHEMA.md](SCHEMA.md)). Replaces the in-memory skeleton.
- [x] **Teacher picks an assignment set when creating a session** — see [`POST /api/sessions`](#sessions-rooms) and [`GET /api/assignmentsets`](#assignments). Backend endpoints implemented (under the old task naming — rename pending); frontend calls the real API (STORIES.md S6).
- [x] **Solo Practice entry point** — join-bar UI decision made and built; no new contract beyond S4's existing `sessionId`-omitted submission (STORIES.md S7).
- [x] **Sample solution reveal** — see [Solution](#solution). Gating rule decided; endpoint not implemented yet (STORIES.md S8).
- [ ] **Resume suggestion** — see [Resume suggestion (planned)](#resume-suggestion-planned). Plan only — not built (STORIES.md S9).
- [ ] **Session lifetime** — when does a room end (teacher ends it / idle timeout)?
- [ ] **`ProgressUpdated` broadcast** — teacher sees live per-assignment progress, not just who's online (backlog in STORIES.md).

See [SCHEMA.md → Open decisions](SCHEMA.md#open-decisions) for persistence-layer
items that don't affect the wire format (e.g. manual review for `project`
submissions, `AssignmentSet` labeling).
