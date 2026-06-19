# API Contract

The agreement between the **frontend** (React + Monaco) and the **backend** (ASP.NET + SignalR).

> **Rule:** the frontend owner changes this file *first*; the backend owner builds to match it.
> As long as both sides honor what's written here, frontend and backend can be
> developed in parallel — each mocking the other against this contract.

---

## Design decision: `execute` vs `submission`

There are **two separate concerns**, and they get **two separate endpoints**:

- **`execute`** — "What does this code do?" Stateless: code in, output out.
  Knows nothing about students or tasks. Called constantly (every "Run" click).
- **`submission`** — "Did this student complete this task?" Stateful: tied to a
  student + task + progress. Called once, when the student thinks they're done.
  **Built on top of `execute`** (it runs the code, then records the result).

`execute` is fully defined. `submission` is deferred until we build the
tasks/progress feature — see [Open decisions](#open-decisions).

User stories that drive these features live in [STORIES.md](STORIES.md).

---

## Identity (no registration)

Students are **anonymous but persistent**. No login, no password, no email.

- On first visit the client generates a `studentId` (UUID) and stores it in
  `localStorage`, along with a `displayName` the student types once.
- Every request / connection carries the `studentId`.
- The **server** stores progress keyed by `studentId`. localStorage holds the
  *key*; the server holds the *data*.

```
studentId:    "uuid-v4"          // durable identity (localStorage + server progress)
displayName:  "Maria"            // a label, NOT auth — shown on the teacher dashboard
role:         "student" | "teacher"
```

Tradeoff we accept: identity is **device/browser-bound**. Clearing the browser
or switching laptops loses the key. Fine for a 3-day workshop.

> `studentId` (who you are) and **session membership** (which live room you're in)
> are different things with different lifetimes — see [Sessions](#sessions-rooms).
> `execute` and `submission` only need `studentId`; only *broadcasts* are session-scoped.

---

## Sessions (rooms)

Two populations use the site:

- **Live cohort** — students physically in the workshop, joined to the teacher's room.
- **Solo cohort** — students given the link later, working self-paced, in no room.

A **room is a SignalR Group** named by a short session `code`. Broadcasts (e.g. the
timer) go to the group, so solo students never receive them — confusion solved by
construction, not by asking people to ignore things.

```
code:   "ABCD"   // 4–6 uppercase chars, skip ambiguous 0/O 1/I
                 // unique among *active* sessions only; regenerate on collision
```

Session state lives **in-memory** on the server (ephemeral — if the server restarts,
the teacher re-creates the room). Student *progress* is persisted separately, so a
restart never loses progress.

### `POST /api/sessions`  (teacher creates a room)
```json
// → 200 OK
{ "code": "ABCD" }
```

### `JoinSession` — SignalR hub method (student joins a room)
```
JoinSession({ code, studentId, displayName })
```
- Server adds the connection to Group `code`.
- Server replies to the caller with the current state, so a late joiner / reconnect
  syncs immediately:
```json
// SessionState (reply to caller only)
{ "activeTimer": { "endsAt": "2026-06-19T14:30:00Z" } }   // activeTimer omitted if none
```

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

| Field | Type | Notes |
|---|---|---|
| `code` | string | The full contents of one file. Beginners write single-class programs (`public class Main { ... }`), so one file is enough. |

> Language is **implicit** — the backend is Java-only for now (it hardcodes `java`).
> If more languages are ever needed, add a `language` field rather than reusing `code`;
> that's a deliberate future change, not a silent one.

### Response — `200 OK`
```json
{
  "status": "success",
  "stdout": "Hello World!\n",
  "stderr": ""
}
```

| Field | Type | Notes |
|---|---|---|
| `status` | `"success"` \| `"compile_error"` \| `"runtime_error"` | Tells the frontend how to render (green output vs. red error). |
| `stdout` | string | Program output. Always present (`""` if none). |
| `stderr` | string | Error text. Always present (`""` if none). |

### Worked examples (from the camp slides)

**Success** — `System.out.println(42);`
```json
{ "status": "success", "stdout": "42\n", "stderr": "" }
```

**Compile error** — missing semicolon
```json
{ "status": "compile_error", "stdout": "", "stderr": "Main.java:3: error: ';' expected" }
```

**Runtime error** — e.g. divide by zero
```json
{ "status": "runtime_error", "stdout": "", "stderr": "Exception in thread \"main\" java.lang.ArithmeticException: / by zero" }
```

### Important: HTTP status vs. `status` field

- A student writing broken code is **normal and expected** → still `200 OK`,
  with `status: "compile_error"` or `"runtime_error"`.
- Non-2xx is reserved for **infrastructure** problems only: malformed request
  (`400`), executor container unreachable (`502`/`503`).
- So the frontend renders off the `status` **field**, not the HTTP code.

---

## Timer (teacher → room broadcast)

The teacher's *trigger* is plain REST (a normal request). SignalR is used only for
the *fan-out* to students. So only students need a live connection; the teacher side
stays simple and testable.

### `POST /api/sessions/{code}/timer`  (teacher starts a timer)
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

## Open decisions

Resolve each *in this file* before the relevant feature is built.

- [ ] **`POST /api/submission`** — payload for submitting a task attempt
  (`studentId`, `taskId`, `code` → records progress + result).
- [ ] **Tasks** — how the sidebar gets its task list (static bundle vs. `GET /api/tasks`).
- [ ] **SignalR hub path** — the URL the client connects to (e.g. `/hub`).
- [ ] **Progress → teacher** — real-time `ProgressUpdated` event so the teacher
  dashboard updates live (teacher joins the room as an observer to receive it).
- [ ] **Progress persistence** — store keyed by `studentId` (in-memory for the
  skeleton; a real store later).
- [ ] **Session lifetime** — when does a room end (teacher ends it / idle timeout)?
