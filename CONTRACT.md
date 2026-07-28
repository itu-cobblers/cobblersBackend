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
{ "activeTimer": { "endsAt": "2026-06-19T14:30:00Z", "assignmentId": 101 }, "focusedAssignmentId": 101 }
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

**Scoped to one `assignmentId`, not the whole room.** A room has at most one
*active* timer at a time — starting a new one (for the same or a different
`assignmentId`) simply replaces it; there's no per-assignment concurrency.
This matters for the student UI: a countdown badge only shows on the
assignment whose `assignmentId` matches the active timer.

**Purely a pacing display — it never gates [Solution](#solution) reveal.**
An earlier design tied the `code`/`project` reveal rule to this timer (room
students only saw the answer once the countdown was low); that's been
dropped (see [SCHEMA.md](SCHEMA.md#solution-reveal-moved-entirely-to-the-frontend)).
The timer answers "how much longer on this question," full stop — reveal is
submission-based end to end, same in solo and in a room.

### `POST /api/sessions/{code}/timer` (teacher starts a timer)

```json
// request
{ "durationMinutes": 10, "assignmentId": 101 }

// → 200 OK  — server computes the absolute end time, stores it (with the
//             assignmentId) on the session, then broadcasts TimerStarted to
//             Group {code}
{ "endsAt": "2026-06-19T14:30:00Z", "assignmentId": 101 }
```

> `durationMinutes: 0` is a valid, if unusual, input — just an
> instantly-elapsed timer. It has no special meaning; there used to be a
> teacher "reveal this project's answer now" action that reused this
> endpoint with `durationMinutes: 0`, but that's retired — see
> [Solution](#solution).

### `TimerStarted` — SignalR event (server → students in the room)

```json
{ "endsAt": "2026-06-19T14:30:00Z", "assignmentId": 101 }
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

## Teacher dashboard hydration (attendance + progress)

"Who's here, and how far did each of them get?" — the teacher-side counterpart
to the student's [`SessionState`](#joinsession-signalr-hub-method-student-joins-a-room)
reply. `ObserveSession` alone only ever answers "who's connected **right
now**" (the live, in-memory roster) — a teacher who reloads the dashboard,
reconnects, or whose server process restarted mid-class has no way to
recover *who attended* or *what they'd already passed*, since neither of
those lives on the SignalR connection. These two `GET` endpoints are the
**REST hydration layer**: called once when the dashboard mounts (or
reconnects), *before* calling `ObserveSession` for the live delta on top.
See [STORIES.md](STORIES.md) S10.

> **Persisted, not live.** Both endpoints read `Attendance`/`Submission`
> tables (see [SCHEMA.md](SCHEMA.md)) — the historical record, which
> survives a server restart. This is a deliberately **different read** from
> `ObserveSession`'s in-memory `SessionStore` roster; don't conflate "who
> attended" with "who's connected this second." A student who joined, then
> closed their laptop, still shows up here.

### `GET /api/sessions/{code}/attendance` (teacher — roster hydration)

```json
// → 200 OK
[
  { "studentId": "uuid", "displayName": "Maria" },
  { "studentId": "uuid", "displayName": "Jonas" }
]
```

The set of students **currently active** in this session — i.e. joined and
not yet left/removed (see `Attendance` in [SCHEMA.md](SCHEMA.md) for what
flips a row inactive). Same shape as `ObserveSession`'s reply, but backed by
the persisted table instead of the in-memory roster, so it survives a
reload/reconnect/restart. This list is also the **denominator** for
`passedNum`/`totalNum` on [`/progress`](#get-apisessionscodeprogress-teacher--per-assignment-per-student-status)
below and for the teacher assignment list's pass-rate badge (Col 1) — a
student who left the room stops counting toward "how many passed," even
though their `Submission` rows aren't deleted.

`404 Not Found` if `code` doesn't resolve to any session (same rule as
[`GET /api/sessions/{code}`](#get-apisessionscode-room-cohort-resolve-the-rooms-assignment-set)) —
including an `ended` one, since there's nothing live left to hydrate.

### `GET /api/sessions/{code}/progress` (teacher — per-assignment × per-student status)

```json
// → 200 OK
[
  { "assignmentId": 101, "studentId": "uuid-maria", "status": "passed" },
  { "assignmentId": 101, "studentId": "uuid-jonas", "status": "untried" },
  { "assignmentId": 118, "studentId": "uuid-maria", "status": "failed" }
]
```

| Field          | Type                                    | Notes                                                                                                                      |
| -------------- | ---------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `assignmentId` | number                                   | One of the session's `assignmentSetId`'s assignments.                                                                       |
| `studentId`    | string                                    | One of the session's currently-active students (same population as `/attendance`).                                        |
| `status`       | `"passed"` \| `"failed"` \| `"untried"` | See derivation below.                                                                                                       |

**Derivation** (per `(assignmentId, studentId)` pair, mirrors S5's "any
attempt" rule — see [`GET /api/students/{studentId}/submissions`](#get-apistudentsstudentidsubmissions)):

- `"passed"` — **at least one** `Submission` for this pair has `passed = true`.
- `"failed"` — at least one `Submission` exists, but **none** has `passed = true`
  (covers `passed = false` and the `predict`/`code` "tried and missed" case).
- `"untried"` — **no** `Submission` row exists for this pair yet.

`kind: "project"` assignments never appear in this array — they have no
`Submission` at all (see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)),
so there's no status to report; the frontend should treat a missing pair the
same as `"untried"` rather than expecting an explicit row for every
assignment × student combination. `passed = null` (an assignment kind with
no automated grader) is likewise excluded — never emitted as `"passed"` or `"failed"`.

**Frontend usage:** feeds both `passedNum`/`totalNum` on each row of Col 1
(`TeacherProblemsList` — count `status: "passed"` divided by
`/attendance`'s length) and the per-student dot color in Col 1/Col 2 once a
specific assignment or student is selected (`TeacherProblemItem.studentStatus`,
`AttendanceStudent.assignmentStatus`). The response is intentionally **flat**
(not pre-grouped by assignment or by student) — the frontend already needs
both groupings (by-assignment for Col 1, by-student for Col 2) depending on
which side has a selection, so a single flat list lets it index into
whichever shape it needs rather than the backend guessing.

`404 Not Found` under the same rule as `/attendance` above.

### `GET /api/sessions/{code}/assignments/{assignmentId}/submissions` (teacher — submission history for one assignment in this room)

```json
// → 200 OK
[
  { "subId": "uuid-1", "studentId": "uuid-maria", "displayName": "Maria", "passed": true, "submittedAt": "2026-06-19T14:28:00Z" },
  { "subId": "uuid-0", "studentId": "uuid-maria", "displayName": "Maria", "passed": false, "submittedAt": "2026-06-19T14:25:00Z" }
]
```

Optional query: `?studentId={studentId}` — filters to that one student's full
attempt history for this assignment (the "pick a question + a student"
drill-down). Omit it to get **every** active student's attempts for this
assignment.

| Field         | Type      | Notes                                                                                                                |
| ------------- | --------- | ------------------------------------------------------------------------------------------------------------------- |
| `subId`       | string    | Same id as `submission`'s response — the key into [`GET /api/submissions/{subId}`](#get-apisubmissionssubid-shared--full-detail-for-one-submission) for the code + result replay. |
| `studentId`   | string    |                                                                                                                        |
| `displayName` | string    | Denormalized onto each row so the frontend never needs a second lookup against `/attendance` to label a row.        |
| `passed`      | boolean?  | `null` for a kind with no automated grader.                                                                          |
| `submittedAt` | string    |                                                                                                                        |

This list is deliberately **thin, same as the student's own history below** —
no `content`/`result` per row. Col 3 renders straight off this list (one row
per attempt); Col 4 only needs one submission's code + result at a time (the
one the teacher clicked), so shipping every historical attempt's full source
on every list load would be pure waste. See
[`GET /api/submissions/{subId}`](#get-apisubmissionssubid-shared--full-detail-for-one-submission)
below for how Col 4 actually gets the code.

**Sort order:** newest-first (`submittedAt` desc) — same convention as the
student history endpoint. **Grouping:** the response is a flat list, sorted
by `submittedAt` only (not pre-grouped by `studentId`) — when called without
`?studentId`, the frontend groups client-side to build Col 1/Col 2's
attempt-by-attempt views; the `?studentId` filter exists precisely so the
common single-student drill-down doesn't have to fetch-then-discard everyone
else's rows.

`404 Not Found` under the same session-lookup rule as `/attendance` and
`/progress` above; an `assignmentId` that isn't part of this session's
`assignmentSetId` also `404`s (not a `200` with an empty array — it's an
addressing error, not "no submissions yet").

---

## Live progress broadcasts (server → teacher observers in the room)

The three `GET`s above cover the **snapshot** a teacher dashboard hydrates
on load/reconnect. These two SignalR events are the **live delta** on top —
the same REST-hydrate-then-subscribe split the timer and follow features
already use, so the dashboard never has to re-poll (2)/(3) after a student
does something. Both attach to the same `ObserveSession` subscription
pipeline the roster already uses, and **replace** `StudentJoined` /
`RosterUpdated`'s job going forward — those two only ever carried
`{ studentId, displayName }` with no notion of active/inactive or grading
outcome, which is exactly the gap these two close. Not optional — required to
close S10's live half (see [STORIES.md](STORIES.md) S10 / Backlog).

### `AttendanceUpdated` (a student joins or leaves the room)

```json
{ "studentId": "uuid", "displayName": "Maria", "isActive": true }
```

Broadcast to every observer in Group `code` whenever a student's `Attendance`
row flips active/inactive — joining (`isActive: true`, superseding
`StudentJoined`) or leaving/disconnecting (`isActive: false`, superseding the
"someone left" half of `RosterUpdated`). The teacher dashboard applies this
as an incremental patch to the roster it hydrated from
[`/attendance`](#get-apisessionscodeattendance-teacher-roster-hydration) —
add/update the row by `studentId` — and recomputes `/progress`'s denominator
(`totalNum` on Col 1) from the new active count, **without** re-fetching
`/progress` itself.

### `ProgressUpdated` (a student's submission is graded)

```json
{ "studentId": "uuid", "assignmentId": 101, "status": "passed" }
```

Broadcast to every observer in Group `code` whenever
[`POST /api/assignments/{assignmentId}/submissions`](#post-apiassignmentsassignmentidsubmissions)
finishes grading a submission from a student in this room (`status` uses the
same two-value-only rule as [`/progress`](#get-apisessionscodeprogress-teacher--per-assignment-per-student-status):
a submission always produces `"passed"` or `"failed"`, never `"untried"` —
that value only exists as the *absence* of a submission). The dashboard
applies this as a point patch: update that one cell's status in whatever it
hydrated from `/progress` (recomputing Col 1's `passedNum` badge for
`assignmentId`, and — if that `(studentId, assignmentId)` pair is the
currently-selected Col 1/Col 2 combination — the status dot color) instead of
re-fetching the whole `/progress` array. Never broadcast for a `project`
submission — there is none to broadcast (see
[Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)).

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

### `GET /api/submissions/{subId}` (shared — full detail for one submission)

```json
// → 200 OK
{
  "subId": "uuid",
  "studentId": "uuid",
  "assignmentId": 101,
  "sessionId": "ABCD",
  "content": "public class Main {\n  public static void main(String[] args) {\n    System.out.println(\"Hello ITU!\");\n  }\n}",
  "result": { "status": "success", "stdout": "Hello ITU!\n", "stderr": "" },
  "passed": true,
  "submittedAt": "2026-06-19T14:28:00Z"
}
```

| Field       | Type                           | Notes                                                                                                         |
| ----------- | ------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `content`   | string \| `{name, content}[]`   | Same shape as the [submission request's `content`](#post-apiassignmentsassignmentidsubmissions).                |
| `result`    | object?                          | Same shape as `execute`'s response. `null` for `predict` (nothing executed).                                    |
| `sessionId` | string \| null                  | The room's join code, same convention as [`GET /api/students/{studentId}/submissions`](#get-apistudentsstudentidsubmissions). `null` for a solo submission. |

**One endpoint, two callers.** `subId` is a globally unique surrogate key
(`Guid`) — looking one up needs no other scoping, so this single route serves
both:

- **The student's own "My Progress" review** — after listing their thin
  history via [`GET /api/students/{studentId}/submissions`](#get-apistudentsstudentidsubmissions),
  clicking an attempt calls this to show that attempt's code + result.
- **The teacher's Col 4 replay** — after listing a room's attempts via
  [`GET /api/sessions/{code}/assignments/{assignmentId}/submissions`](#get-apisessionscodeassignmentsassignmentidsubmissions-teacher--submission-history-for-one-assignment-in-this-room),
  clicking a row calls the exact same endpoint.

Both list endpoints already carry `subId` on every row specifically so
they can hand it straight to this one — that's *why* they stay thin instead
of embedding `content`/`result` themselves. No separate teacher-only
detail route; the two list endpoints are what differ (whose history, and
whether it's session-scoped), not how a single submission's full record is
fetched.

> **No access-control boundary today.** Like the rest of this contract
> (anonymous `studentId`, no login), this endpoint doesn't check that the
> caller "owns" `subId` or is a recognized teacher — it's a public read by an
> unguessable `Guid`. Fine for a 3-day workshop POC with no sensitive data;
> revisit if this app ever needs a real per-role authorization layer.

`404 Not Found` for an unknown `subId`.

---

## Solution

Reveal an assignment's sample/reference solution. **Deliberately generic and
gate-free on the backend** — see
[SCHEMA.md](SCHEMA.md#sample-solution-reveal-uses-one-rule-for-both-solo-and-classroom)
for the superseded server-side rule and why reveal timing moved to the
frontend entirely.

### `GET /api/assignments/{assignmentId}/solution`

```json
// → 200 OK — Assignment.SampleSolutionJson is set
{ "available": true, "solution": "public class Main {...}" }

// → 200 OK — no sample solution stored for this assignment
{ "available": false, "solution": null }

// → 404 Not Found — unknown assignmentId
```

| Field       | Type                                     | Notes                                                                                                                                             |
| ----------- | ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `available` | boolean                                   | `true` iff `Assignment.SampleSolutionJson` is non-null. **No other check** — no `studentId`, no submission lookup, no `kind` branch, no timer/session awareness. |
| `solution`  | string \| `{name, content}[]` \| null     | Present only when `available`. Shape matches `Assignment.SampleSolutionJson` as stored (single string for single-file `code`, `{name,content}[]` for multi-file `code`/`project`). |

> **No access-control boundary, by design.** Unlike the earlier "at least one
> submission" server-side gate this replaces, *when* a student is allowed to
> see the answer is decided entirely by the frontend (see below) — this
> endpoint answers only "does a solution exist," the same way `GET
> /api/submissions/{subId}` has no ownership check (see
> [Submission](#submission)). A student opening devtools can call this early
> and see the answer before the frontend would normally reveal it; accepted
> for a 3-day workshop POC with no grading stakes tied to secrecy.
>
> **Frontend reveal rules, per `kind` — same rule in solo and in a room, no
> [Timer](#timer-teacher--room-broadcast) involved at all:**
> - `code`: after the student has submitted this assignment at least once.
> - `predict`: submitted at least once. In practice `predict` never calls
>   this endpoint at all; its answer is `content.expectedOutput`, already
>   sent with the assignment.
> - `project`: always available immediately — a `project`
>   [never produces a `Submission`](#mini-projects-are-vs-code-only), so
>   there's no "submitted once" signal to gate on, same reasoning as solo
>   `code` would use if it had none either.
>
> An earlier iteration tied the `code`/`project` room rule to the per-assignment
> Timer (answer opens at ≤3 min remaining for `code`; teacher-triggered via a
> `durationMinutes: 0` timer for `project`) instead of the submission check.
> That's been dropped — see
> [SCHEMA.md](SCHEMA.md#solution-reveal-moved-entirely-to-the-frontend) for
> why: tying reveal to the room's *one* active timer meant a student's
> unlocked state depended on which assignment the timer currently happened to
> be scoped to, which doesn't survive a refresh or the teacher starting a new
> timer for a different assignment without extra client- or server-side
> persistence. Keying reveal off `submissionHistory` instead avoids that
> entirely — it's data the backend already persists, so there's nothing new
> to keep in sync.

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
- [x] **Teacher dashboard hydration + live progress** — see [Teacher dashboard hydration](#teacher-dashboard-hydration-attendance--progress) and [Live progress broadcasts](#live-progress-broadcasts-server--teacher-observers-in-the-room). Contract decided (3 `GET`s + `AttendanceUpdated`/`ProgressUpdated`, superseding `StudentJoined`/`RosterUpdated`); none implemented yet (STORIES.md S10).
- [x] **Submission detail (code + result replay)** — see [`GET /api/submissions/{subId}`](#get-apisubmissionssubid-shared--full-detail-for-one-submission). One shared, unscoped-by-role endpoint for both the student's own "My Progress" review and the teacher's Col 4 replay; both `GET .../submissions` list endpoints stay thin (`subId` + outcome only) and hand off to this one. Not implemented yet.

See [SCHEMA.md → Open decisions](SCHEMA.md#open-decisions) for persistence-layer
items that don't affect the wire format (e.g. `AssignmentSet` labeling).
