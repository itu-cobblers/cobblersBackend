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

### `PUT /api/students/{studentId}` (register/update a display name)

```json
// request
{ "displayName": "Maria" }

// → 204 No Content
```

Upserts the student row server-side. **Required before any `Submission` can
be written** — `POST /api/assignments/{assignmentId}/submissions` rejects a
`studentId` it doesn't recognize (see api repo CLAUDE.md, and
[Submission](#submission) below). The frontend calls this once per
join/solo/rehydrate (`@lib/studentApi.upsertStudent`), best-effort — a
failure here doesn't block reaching the IDE; it just means the *next*
submission will 400, which the submit flow already renders as "not
submitted" rather than crashing.

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

A session has a **`status`**: `"active"` (default, from creation) or `"ended"`.
See [SCHEMA.md](SCHEMA.md#sessionstatus) for the column. All session-lookup
endpoints below (`GET /api/sessions/{code}`, `GET /api/sessions/today-latest`)
only ever consider `active` sessions — an ended room is invisible to new joins,
even though its historical `Attendance`/`Submission` rows are untouched.

### `POST /api/sessions/{code}/end` (teacher manually ends a room)

```json
// no request body

// → 204 No Content
```

- Sets `Session.Status = "ended"` in the database.
- Clears the room's in-memory `SessionStore` entry (`SessionStore.RemoveRoom`)
  — the ephemeral roster/timer for that room stop existing.
- **Broadcasts** `SessionEnded` (no payload) to every connection in Group `code`.
- `404 Not Found` if `code` doesn't resolve to any session at all (never
  existed). Ending a session that's already `ended` still succeeds (`204`) —
  there's no "already ended" error, since the desired end state is identical.

### `SessionEnded` — SignalR event (server → students in the room)

```json
// no payload
```

The student side treats this the same as "the room is gone": it drops any
local session/room state and **navigates back to the entry screen** — never a
silent no-op, since continuing to render a room that no longer exists (e.g. a
teacher could later reuse the code's assignment set for a new session) would
be misleading. Solo (off-site) students never receive this — no room, no group.

### `GET /api/sessions/today-latest` (student entry screen — is a session live today)

```json
// → 200 OK — an active session was created today
{ "code": "ABCD", "assignmentSetId": "day1-2026" }

// → 404 Not Found — no active session created today
```

Called once, unauthenticated, when the entry screen mounts (`@lib/sessionApi.fetchTodayLatestSession`)
— no `studentId` involved, since this isn't personalized (unlike the retired
[Resume suggestion](#resume-suggestion-retired) design below, this doesn't care
whether *this* student already attended). "Today" is server UTC-midnight to
now; if more than one session was created today, the most recently created
`active` one wins (same tie-break as `today-latest` sharing its query shape
with the retired [resume-suggestion heuristic](SCHEMA.md#welcome-back-resume-suggestion-retired--superseded-by-today-latest)).

**Frontend flow:** the entry screen's "Join current Session" button is
disabled with "No current active session to join" while this resolves to
`404`/no session, and enabled with the `code` badge shown inline once one
exists — clicking it calls `JoinSession` with that `code` directly, same as
if the student had typed it. A `404` here is an expected, non-error outcome
("nothing to join right now"), not a fault — the frontend treats it exactly
like any other non-2xx/network error: "no session today" (button stays
disabled). This endpoint is a convenience, never a hard blocker to Solo Practice.

### `JoinSession` — SignalR hub method (student joins a room)

```
JoinSession({ code, studentId, displayName })
```

- Server adds the connection to Group `code`.
- Server replies to the caller with the current state, so a late joiner / reconnect
  syncs immediately:

```json
// SessionState (reply to caller only)
{ "activeTimer": { "endsAt": "2026-06-19T14:30:00Z" }, "focusedAssignmentId": 101 }
 // activeTimer / focusedAssignmentId omitted if none — see Follow below
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

> Only resolves **active** sessions — `404` for an unknown code *or* a code
> that exists but has `Status = "ended"` (see [above](#post-apisessionscodeend-teacher-manually-ends-a-room)).
> A student mid-session whose teacher ends the room is redirected out via the
> `SessionEnded` broadcast, not by this endpoint 404-ing on the next call.

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
| `kind`    | `"code"` \| `"predict"` \| `"project"` | `"project"` is content-only for now — see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only). The frontend should render its `brief`/`lesson` but **no editor, no Run, no Submit**. |
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
| `files`      | `{name, content}[]`? | Multi-file run. Each item is one source file. Used for the Day-3 single-class assignments (student's class + a hidden grader `Main`) — see the harness note below.                     |
| `entryClass` | string?              | When `files` is given, the class whose `main` to run (e.g. `"Main"`).                                                                                                                  |
| `stdin`      | string?              | Standard input piped to the program. Defined for future interactive programs, but **nothing currently uses it** — see the [Scanner / interactive input](#scanner--interactive-input-is-out-of-scope) note below. Omit/`""` when none.                                                                  |

> **Multi-file execution (`execute` & `submission`).**
> Send `code` for one file, or `files` + `entryClass` for several.
> `code: X` is equivalent to `files: [{name:"Main.java", content:X}], entryClass:"Main"`.
>
> For Day 3 multi-file assignments (e.g. `person-class`, `flight-ticket-class`, `container-class`),
> the frontend provides a multi-tab editor (`Main.java` + student's class) and calls `execute` with `files`
> containing both student-editable files.
>
> Language is **implicit** — the backend is Java-only for now (it hardcodes `java`).

The **response** shape is unchanged (`status` / `stdout` / `stderr`) regardless of single- or multi-file input. The executor only compiles + runs — it never grades. Grading happens server-side via [Submission](#submission).

> **Multi-file student submission (Day 3 practice, e.g. `person-class` /
> `flight-ticket-class` / `container-class`).**
> When the student works on a multi-file assignment with a multi-tab editor,
> the frontend submits the full file array in `content: [{ name, content }]`.
> The backend passes these files directly to `ExecutorService.ExecuteAsync` and evaluates
> `GradingJson` on the result.

### Scanner / interactive input is out of scope

No assignment served today asks the student for `stdin`, and none is planned to.
Two precedents:

- The Day-2 `guess-locker` / `how-many-ab` drafts (guess-the-number, bulls-and-cows)
  were rewritten to use a **fixed-seed `Random`** instead of `Scanner` (see
  `analog-reusable-cup-stamps` / `beerpong-at-scrollbar` in
  `scripts/seed-tasks.sql`) — same "guess and branch" pedagogy, but
  deterministic and gradable without any input at all.
- Of the three Day-3 mini-projects, **all three are `kind: "project"` and none
  are run/submitted through this app** — see
  [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only) below. That
  decision was made *because* some of them need `Scanner`, but it's written as
  "the whole `project` kind is out of scope," not "these specific two are" —
  see that section for why.

`stdin` therefore stays defined on `execute` (Piston supports it, and a truly
non-interactive canned input — the whole input fed in upfront, not a live
back-and-forth — is easy to add later for a single-file `code` assignment) but
is **not wired to anything today**. If a future assignment wants it, add its
`ContentJson.stdin` and start populating this field; don't build interactivity
into the frontend for it — `execute` is one request/response, it can't react
to the program's output mid-run.

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

## Follow (teacher → room broadcast)

Unlike the timer, both the trigger and the fan-out go over SignalR — the teacher
already holds a live hub connection (`ObserveSession`), so there's no need for a
separate REST endpoint.

### `FocusAssignment` — SignalR hub method (teacher moves to an assignment)

```
FocusAssignment(code, assignmentId)
```

- Server stores `assignmentId` as the room's focused assignment (so late joiners /
  reconnects sync — see `focusedAssignmentId` on `SessionState` above), then
  **broadcasts** `AssignmentFocused` to every connection in Group `code`.
- No reply value — this is fire-and-forget from the teacher's point of view.

### `AssignmentFocused` — SignalR event (server → students in the room)

```json
101
```

Just the bare `assignmentId` (a number), not wrapped in an object.

The student side is **non-coercive**, same spirit as the timer: it shows a
"teacher is on _X_ — Follow?" banner rather than force-navigating. The student
decides whether to jump. Solo (off-site) students never receive this — no room,
no group.

---

## Mini-projects are VS-Code-only

All three Day-3 mini-projects (`build-a-tree`, `grandpas-time-machine`,
`grandmas-blackmarket-kitchen` — `kind: "project"`) are **entirely out of
scope** for `execute`/`submission`. Students read the `brief` in the app, then
write, run, and test the code locally in VS Code. Nothing about a project is
ever sent back to this backend.

This is **not** "2 of 3 need `Scanner`, 1 doesn't, so 2 are VS-Code-only and 1
stays online." It's all-or-nothing for the whole `project` kind, because the
three projects are **alternatives students pick from in the same time slot**
(not all three, back-to-back) — if only the Scanner-free project had a working
online judge, the in-app experience would silently differ by which project a
student happened to choose. Kind-level scope is also one rule to implement and
explain instead of a per-assignment flag that has to be re-decided every time a
project is added or edited.

Practically:

- `GET /api/assignmentsets/{assignmentSetId}/assignments` still returns
  `project` items (title/brief/lesson) — the frontend still needs to *display*
  them, just without an editor or Run/Submit affordance.
- `POST /api/execute` and `POST /api/assignments/{assignmentId}/submissions`
  are never called for a `project` assignment. Neither endpoint needs to
  reject `kind: "project"` today (nothing calls them that way), but if a
  defensive check is ever added, this is why.
- There is currently **no completion signal at all** for a project — no
  `Submission` row, so nothing shows up in
  [`GET /api/students/{studentId}/submissions`](#submission) for it. That's
  an accepted gap for now (see [Open decisions](#open-decisions)), not
  something the frontend needs to work around.
- This doesn't cost any automated-grading capability that existed before:
  `Project` had no automated grader even when it was still nominally in scope
  (`Submission.Passed` stays `null` — see
  [SCHEMA.md](SCHEMA.md#grading-rules-are-data-evaluated-by-one-backend-engine)).
  What's newly out of scope is *running the code and seeing the output* in
  the app, not grading.

---

## Submission

"Did this student complete this assignment?" One endpoint for `code`/`predict`
(not `project` — see [above](#mini-projects-are-vs-code-only)), for both the
room cohort and the solo cohort (`sessionId` is optional — see
[SCHEMA.md](SCHEMA.md#sessionid-is-nullable-on-submission)). Built on top
of `execute` for `code`; `predict` never touches the executor.

Grading is **server-side now**, not client-reported — see
[SCHEMA.md](SCHEMA.md#grading-rules-are-data-evaluated-by-one-backend-engine). The
frontend's `check()` no longer decides `passed`.

### `POST /api/assignments/{assignmentId}/submissions`

```json
// request — single-file code or predict
{ "studentId": "uuid", "sessionId": "ABCD", "content": "public class Main {...}" }

// request — multi-file code (Day 3, e.g. `person-class` with editable Main.java + Person.java)
{
  "studentId": "uuid",
  "sessionId": "ABCD",
  "content": [
    { "name": "Main.java", "content": "public class Main { ... }" },
    { "name": "Person.java", "content": "public class Person { ... }" }
  ]
}

// request — solo/practice (no room joined)
{ "studentId": "uuid", "content": "public class Main {...}" }
```

| Field       | Type                         | Notes                                                                                                                                                                                                                                                                                 |
| ----------- | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `studentId` | string                       | Required.                                                                                                                                                                                                                                                                            |
| `sessionId` | string?                      | Omit for solo/practice submissions made without joining a room.                                                                                                                                                                                                                      |
| `content`   | string \| `{name, content}[]`| A string for single-file `code` / `predict`; a file list `[{name, content}]` for multi-file `code` assignments (e.g. `person-class`, `flight-ticket-class`, `container-class`). Never called for `project` — see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only). |

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
| `passed` | boolean? | Server-computed. `null` for any assignment without an automated grader. (`project` never reaches this response at all — see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only).) |
| `result` | object?  | Present for `code` (same shape as `execute`'s response). `null` for `predict` — nothing is executed. |

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

`sessionId` here is the room's **join code** (`Session.Code`, e.g. `"ABCD"`),
not an internal id — same shape a student typed to join, so the frontend can
label an attempt "Room ABCD" vs "Solo" without a second lookup. `null` for a
solo/practice submission. Deliberately thin: no `content`/`result` — see
[SCHEMA.md](SCHEMA.md#submission) for why this stays a lightweight list, not
a full replay of each attempt.

> **Status: frontend built, backend not yet implemented** (STORIES.md S5).
> The frontend's "My Progress" panel (`ProgressModal`, opened from the
> Toolbar or the entry screen) calls this on load and groups the response by
> `assignmentId` — showing every attempt, but the assignment-level status is
> "passed" if **any** attempt has `passed: true` (not the latest attempt,
> not an average). It also seeds which assignments already show as done in
> the stepper across a reload or the next day's session. Until the backend
> route exists, every 404/network error is treated as "no history yet" — an
> empty list, not an error banner — so this ships ahead of the backend
> without breaking the rest of the app. Remove that fallback expectation
> once the endpoint is live and a 404 means something again.

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
>
> **`project` never has a `Submission`** (see
> [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)), so this
> gate can never open for one — `available` would stay `false` forever. Don't
> wire a "Show solution" button for `project` assignments at all; showing a
> permanently-disabled button would be confusing, not honest UI.

---

## Resume suggestion (retired)

**Retired — replaced by [`GET /api/sessions/today-latest`](#get-apisessionstoday-latest-student-entry-screen--is-a-session-live-today).**
This section originally planned a *personalized*, per-`studentId` suggestion
(`GET /api/students/{studentId}/resume-suggestion`, matched against that
student's `Attendance` history) surfaced via a dismissible `WelcomeBackBanner`
component. Neither the endpoint nor the component exist anymore.

**Why retired, not just deferred:** the personalization added real complexity
(a query joining `Attendance`, a banner that could appear over either screen,
dismiss-state) for a payoff that didn't matter in practice — the workshop runs
one live class at a time, so "the session I should join" is the same answer
for every student on a given day, not something that needs *this student's*
history to compute. `today-latest` answers the simpler, sufficient question
("is a session live right now?") with no `studentId`, no `Attendance` join,
and no banner — just a button on the entry screen that's enabled/disabled
based on the response. See [STORIES.md](STORIES.md) S9 for the before/after.

The tie-break for "which session, if more than one was created today" is
unchanged from the original design — most recent `CreateAt` wins, now
filtered to `Status = "active"` too (an ended session is never suggested,
even if it's the most recent one created). See
[SCHEMA.md](SCHEMA.md#welcome-back-resume-suggestion-retired--superseded-by-today-latest).

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
- [x] **Mini-projects are VS-Code-only** — see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only). `execute`/`submission` are never called for `kind: "project"`, at all, not per-assignment.
- [x] **Multi-file execution for Day-3 single-class assignments** — see the [harness note](#post-apiexecute) under `execute`. No new wire shape (`files`/`entryClass` was already documented); the open work is `PistonClient` actually sending Piston more than one file (see CLAUDE.md, "Java-only, single-class assumption").
- [x] **Resume suggestion** — **retired**, see [Resume suggestion (retired)](#resume-suggestion-retired). Replaced end-to-end by [`GET /api/sessions/today-latest`](#get-apisessionstoday-latest-student-entry-screen--is-a-session-live-today) (STORIES.md S9).
- [x] **Session lifetime** — a room ends only when the teacher manually ends it (`POST /api/sessions/{code}/end`, see [Sessions](#sessions-rooms)); no idle timeout. `Session.Status` persists the end so it survives a server restart; `SessionStore`'s in-memory roster/timer for that room are cleared at the same time.
- [ ] **`ProgressUpdated` broadcast** — teacher sees live per-assignment progress, not just who's online (backlog in STORIES.md).

See [SCHEMA.md → Open decisions](SCHEMA.md#open-decisions) for persistence-layer
items that don't affect the wire format (e.g. `AssignmentSet` labeling).
