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
// StudentJoined — one student, sent when someone joins (live presence)
{ "studentId": "uuid", "displayName": "Maria" }

// RosterUpdated — the full *connected* list (sent on join / leave / disconnect)
[ { "studentId": "uuid", "displayName": "Maria" } ]
```

A `Student` is `{ studentId: string, displayName: string }`. The teacher dashboard
renders `displayName`s; `studentId` keys them so duplicates merge.

This list is **who's connected right now** (`SessionStore`) — the source of
**green** presence dots on the teacher dashboard. Explicit Leave and hub
disconnect are treated the same: the student drops out of this list (dot
goes gray) but stays on the persisted
[`/attendance`](#get-apisessionscodeattendance-teacher-roster-hydration) roll.
If a `studentId` appears here that is not yet on the hydrated roll (first
join while the teacher is watching), the frontend **appends** them to the
roll and bumps `totalNum` — no separate attendance event. See
[Teacher dashboard hydration](#teacher-dashboard-hydration-attendance--submissions).

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

## Teacher dashboard hydration (attendance + submissions)

"Who's here, and how far did each of them get?" — the teacher-side counterpart
to the student's [`SessionState`](#joinsession-signalr-hub-method-student-joins-a-room)
reply. `ObserveSession` alone only ever answers "who's connected **right
now**" (the live, in-memory roster) — a teacher who reloads the dashboard,
reconnects, or whose server process restarted mid-class has no way to
recover *who attended* or *what they'd already submitted*, since neither of
those lives on the SignalR connection. These two `GET` endpoints are the
**REST hydration layer**: called once when the dashboard mounts (or
reconnects), *before* calling `ObserveSession` for the live delta on top.
See [STORIES.md](STORIES.md) S10.

> **Persisted, not live.** Both endpoints read `Attendance`/`Submission`
> tables (see [SCHEMA.md](SCHEMA.md)) — the historical record, which
> survives a server restart. This is a deliberately **different read** from
> `ObserveSession`'s in-memory `SessionStore` roster; don't conflate "who
> attended" with "who's connected this second." A student who joined, then
> closed their laptop or hit Leave, **still shows up** on `/attendance`.

> **Presence (gray / green) is frontend-only.** `/attendance` is the roll of
> everyone who has ever joined — each name renders with a **gray** presence
> dot by default. A name turns **green** only while that `studentId` appears
> in the live SignalR roster (`ObserveSession` reply / `RosterUpdated`).
> Explicit Leave and hub disconnect are the **same** offline state for
> presence: both drop the student from `SessionStore`, neither deletes their
> `Attendance` row. After a break, the teacher therefore sees the full class
> as gray names until each student reconnects and SignalR paints them green
> again.

> **No `/progress` endpoint.** The teacher dashboard always lands with an
> assignment selected, so it needs the thin attempt list on mount anyway —
> not as a lazy drill-down. Per-`(assignmentId, studentId)` status
> (`passed` / `failed` / `untried`), Col 1's `passedNum`/`totalNum`, and
> status dots are all **derived on the frontend** from
> [`/attendance`](#get-apisessionscodeattendance-teacher-roster-hydration) +
> [`/submissions`](#get-apisessionscodesubmissions-teacher--all-thin-submissions-in-this-room)
> (+ the session's assignment list). The backend ships the raw attempts;
> it does not pre-aggregate a status matrix.

### `GET /api/sessions/{code}/attendance` (teacher — roster hydration)

```json
// → 200 OK
[
  { "studentId": "uuid", "displayName": "Maria" },
  { "studentId": "uuid", "displayName": "Jonas" }
]
```

Everyone who has **ever joined** this session — one `Attendance` row per
`(studentId, session)`, created on first successful `JoinSession` and
**never removed** for Leave, disconnect, or a temporary laptop close (see
[`Attendance`](SCHEMA.md#attendance) in [SCHEMA.md](SCHEMA.md)). Same shape
as `ObserveSession`'s reply, but backed by the persisted table instead of
the in-memory roster, so it survives a reload/reconnect/restart. This list
is the **denominator** for the frontend-derived pass-rate badge on Col 1
(`passedNum` / `totalNum`) for the whole class roll — leaving or going
offline does **not** shrink `totalNum`. Label rows in Col 3 by joining
`studentId` → `displayName` from this list (the submissions endpoint does
not denormalize names).

`404 Not Found` if `code` doesn't resolve to any session (same rule as
[`GET /api/sessions/{code}`](#get-apisessionscode-room-cohort-resolve-the-rooms-assignment-set)) —
including an `ended` one, since there's nothing live left to hydrate.

### `GET /api/sessions/{code}/submissions` (teacher — all thin submissions in this room)

```json
// → 200 OK
[
  { "subId": "uuid-1", "assignmentId": 101, "studentId": "uuid-maria", "passed": true,  "submittedAt": "2026-06-19T14:28:00Z" },
  { "subId": "uuid-0", "assignmentId": 101, "studentId": "uuid-maria", "passed": false, "submittedAt": "2026-06-19T14:25:00Z" },
  { "subId": "uuid-2", "assignmentId": 118, "studentId": "uuid-jonas", "passed": true,  "submittedAt": "2026-06-19T14:20:00Z" }
]
```

Every `Submission` row tagged to this session — **all assignments, all
students, every attempt** — in one flat list. No
`/assignments/{assignmentId}/…` scoping and no query filters: the dashboard
hydrates once and filters/groups client-side for whichever assignment (and
optionally student) is selected.

| Field          | Type     | Notes                                                                                                                |
| -------------- | -------- | -------------------------------------------------------------------------------------------------------------------- |
| `subId`        | string   | Same id as `submission`'s response — the key into [`GET /api/submissions/{subId}`](#get-apisubmissionssubid-shared--full-detail-for-one-submission) for the code + result replay. |
| `assignmentId` | number   | Which assignment this attempt belongs to.                                                                            |
| `studentId`    | string   | Join against [`/attendance`](#get-apisessionscodeattendance-teacher-roster-hydration) for `displayName`.             |
| `passed`       | boolean? | `null` for a kind with no automated grader.                                                                          |
| `submittedAt`  | string   |                                                                                                                      |

This list is deliberately **thin, same as the student's own history below** —
no `content`/`result` per row. Col 3 filters this list to the selected
assignment (and optionally student) and renders one row per attempt; Col 4
only needs one submission's code + result at a time (the one the teacher
clicked), so shipping every historical attempt's full source on every list
load would be pure waste. See
[`GET /api/submissions/{subId}`](#get-apisubmissionssubid-shared--full-detail-for-one-submission)
below for how Col 4 actually gets the code.

**Sort order:** newest-first (`submittedAt` desc) — same convention as the
student history endpoint. **Grouping:** the response is a flat list, sorted
by `submittedAt` only (not pre-grouped by `assignmentId` or `studentId`) —
the frontend already needs both cuts depending on selection, so one flat
list lets it index into whichever shape it needs.

**Frontend derivation** (per `(assignmentId, studentId)` pair among
everyone on [`/attendance`](#get-apisessionscodeattendance-teacher-roster-hydration)
× non-`project` assignments — mirrors S5's "any attempt" rule):

- `"passed"` — **at least one** row for this pair has `passed = true`.
- `"failed"` — at least one row exists, but **none** has `passed = true`.
- `"untried"` — **no** row exists for this pair yet.

`kind: "project"` assignments never appear in this array — they have no
`Submission` at all (see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)).
Treat a missing pair the same as `"untried"`. `passed = null` rows do not
count as `"passed"` or `"failed"`. Col 1's `passedNum` is the count of
attendees with derived `"passed"` for that assignment; `totalNum` is
[`/attendance`](#get-apisessionscodeattendance-teacher-roster-hydration)'s
length (the full ever-joined roll — offline / Leave does not shrink it).

`404 Not Found` under the same session-lookup rule as `/attendance` above.
An empty array is a valid `200` ("nobody has submitted yet").

---

## Live progress broadcasts (server → teacher observers in the room)

The two `GET`s above cover the **snapshot** a teacher dashboard hydrates
on load/reconnect. Live deltas on top use the same
REST-hydrate-then-subscribe split the timer and follow features already
use, so the dashboard never has to re-poll after a student does something.
They attach to the same `ObserveSession` subscription pipeline the roster
already uses. Not optional — required to close S10's live half (see
[STORIES.md](STORIES.md) S10 / Backlog).

**Presence (and live roll growth) stay on
[`RosterUpdated`](#roster-events-server--teacher-observers-in-the-room) /
`ObserveSession`.** There is no separate attendance SignalR event: a
`studentId` that appears on the connected roster but is not yet in the
hydrated `/attendance` list is appended to the roll (and shown green);
Leave and disconnect only drop them from the connected set (dot goes
gray) — neither deletes the persisted `Attendance` row. Submission
progress has its own event below.

### `SubmissionRecorded` (a student's submission is graded)

```json
{ "subId": "uuid-1", "assignmentId": 101, "studentId": "uuid-maria", "passed": true, "submittedAt": "2026-06-19T14:28:00Z" }
```

Broadcast to every observer in Group `code` whenever
[`POST /api/assignments/{assignmentId}/submissions`](#post-apiassignmentsassignmentidsubmissions)
finishes grading a submission from a student in this room. Payload shape
matches one row of
[`GET /api/sessions/{code}/submissions`](#get-apisessionscodesubmissions-teacher--all-thin-submissions-in-this-room)
— the dashboard **prepends** it to the hydrated list (newest-first) and
re-derives that `(assignmentId, studentId)` cell's status / Col 1's
`passedNum`, instead of re-fetching the whole array. Never broadcast for a
`project` submission — there is none to broadcast (see
[Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)).

> **Renamed from `ProgressUpdated`.** The old event carried a derived
> `{ studentId, assignmentId, status }` cell. Now that status is
> frontend-owned, the live delta is the thin attempt itself.
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
  [`GET /api/sessions/{code}/submissions`](#get-apisessionscodesubmissions-teacher--all-thin-submissions-in-this-room),
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

Returns an assignment's sample/reference solution from `SampleSolutionJson`.
**Reveal gating is a frontend concern** — the student view only enables
"Show solution" after at least one submission; the teacher preview can reveal
at any time. See [SCHEMA.md](SCHEMA.md#sample-solution-reveal-uses-one-rule-for-both-solo-and-classroom).

### `GET /api/assignments/{assignmentId}/solution`

```json
// → 200 OK — single-file code
{ "solution": "public class Main {...}" }

// → 200 OK — multi-file code / project
{ "solution": [{ "name": "Main.java", "content": "..." }] }

// → 200 OK — no reference answer stored for this assignment
{ "solution": null }
```

| Field      | Type                                 | Notes                                                                                                                                             |
| ---------- | ------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `solution` | string \| `{name, content}[]` \| null | Passthrough of `Assignment.SampleSolutionJson`. Shape matches the assignment's `kind`. Not used for `predict` (its `expectedOutput`, from Assignments, already is the answer). |

`404 Not Found` when `assignmentId` does not exist.

> **`project` never has a `Submission`** (see
> [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only)), so the
> student-view reveal gate can never open for one. Don't wire a "Show solution"
> button for `project` assignments at all; showing a permanently-disabled
> button would be confusing, not honest UI.

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
      (see Sessions). Live attempt deltas are `SubmissionRecorded` (see
      [Live progress broadcasts](#live-progress-broadcasts-server--teacher-observers-in-the-room)).
- [x] **Progress persistence** — `Submission` rows, keyed by `studentId` (see
      [SCHEMA.md](SCHEMA.md)). Replaces the in-memory skeleton.
- [x] **Teacher picks an assignment set when creating a session** — see [`POST /api/sessions`](#sessions-rooms) and [`GET /api/assignmentsets`](#assignments). Backend endpoints implemented (under the old task naming — rename pending); frontend calls the real API (STORIES.md S6).
- [x] **Solo Practice entry point** — join-bar UI decision made and built; no new contract beyond S4's existing `sessionId`-omitted submission (STORIES.md S7).
- [x] **Sample solution reveal** — see [Solution](#solution). Gating in the frontend; backend returns `SampleSolutionJson` on request (STORIES.md S8).
- [x] **Mini-projects are VS-Code-only** — see [Mini-projects are VS-Code-only](#mini-projects-are-vs-code-only). `execute`/`submission` are never called for `kind: "project"`, at all, not per-assignment.
- [x] **Multi-file execution for Day-3 single-class assignments** — see the [harness note](#post-apiexecute) under `execute`. No new wire shape (`files`/`entryClass` was already documented); the open work is `PistonClient` actually sending Piston more than one file (see CLAUDE.md, "Java-only, single-class assumption").
- [x] **Resume suggestion** — **retired**, see [Resume suggestion (retired)](#resume-suggestion-retired). Replaced end-to-end by [`GET /api/sessions/today-latest`](#get-apisessionstoday-latest-student-entry-screen--is-a-session-live-today) (STORIES.md S9).
- [x] **Session lifetime** — a room ends only when the teacher manually ends it (`POST /api/sessions/{code}/end`, see [Sessions](#sessions-rooms)); no idle timeout. `Session.Status` persists the end so it survives a server restart; `SessionStore`'s in-memory roster/timer for that room are cleared at the same time.
- [x] **Teacher dashboard hydration + live progress** — see [Teacher dashboard hydration](#teacher-dashboard-hydration-attendance--submissions) and [Live progress broadcasts](#live-progress-broadcasts-server--teacher-observers-in-the-room). Contract decided (2 `GET`s — `/attendance` = ever-joined roll + `/submissions` — + `SubmissionRecorded` for live grading; green/gray presence and live roll growth stay on `ObserveSession` + `RosterUpdated` only; Leave ≡ disconnect for presence, neither deletes `Attendance`; per-cell status is frontend-derived, no `/progress`); none implemented yet (STORIES.md S10).
- [x] **Submission detail (code + result replay)** — see [`GET /api/submissions/{subId}`](#get-apisubmissionssubid-shared--full-detail-for-one-submission). One shared, unscoped-by-role endpoint for both the student's own "My Progress" review and the teacher's Col 4 replay; both `GET .../submissions` list endpoints stay thin (`subId` + outcome only) and hand off to this one. Not implemented yet.

See [SCHEMA.md → Open decisions](SCHEMA.md#open-decisions) for persistence-layer
items that don't affect the wire format (e.g. `AssignmentSet` labeling).
