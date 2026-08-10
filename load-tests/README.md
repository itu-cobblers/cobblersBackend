# Concurrency load test for submissions

This directory contains [k6](https://k6.io) scripts that simulate students pressing **Submit** on the same assignment.

- `submit-spike.js` — 65 students submitting at roughly the same time.
- `submit-stress.js` — ramps up to 65 virtual users by default.
- `submit-ramp-1-65.js` — ramps from 1 to 65 students to find the latency threshold.
- `hub-broadcast-stress.js` — joins a live room over SignalR and measures the `SubmissionRecorded` broadcast fan-out, not just the REST submit call. See [Run the SignalR broadcast fan-out test](#run-the-signalr-broadcast-fan-out-test).

## What it tests

The script exercises the full submission path:

```
PUT  /api/students/{studentId}                     (register each student)
POST /api/assignments/{assignmentId}/submissions    (submit code)
```

That covers:

- ASP.NET Core request handling + DB writes (Postgres)
- Piston Java code execution
- Response serialization

It does **not** test SignalR live broadcasts (`SubmissionRecorded`). If you also want to verify the teacher dashboard stays responsive during the spike, run the script while a teacher is observing the room — or use `hub-broadcast-stress.js` below, which measures that path directly instead of relying on manual observation.

## Install k6

macOS:

```bash
brew install k6
```

Other platforms or Docker:

```bash
# Docker (mounts the script read-only)
docker run --rm -i grafana/k6 run - <load-tests/submit-spike.js
```

See https://k6.io/docs/get-started/installation/ for more options.

## Pick an assignment ID

The test needs a real `assignmentId`. Find one with:

```bash
curl http://localhost:5046/api/assignmentsets
```

Then fetch its assignments:

```bash
curl http://localhost:5046/api/assignmentsets/{setId}/assignments
```

Pick any `kind: 'code'` assignment and note its `id`.

## Run the 65-student spike

Make sure the backend and Piston are running (`dotnet run --project cobblersBackend` and the Piston container).

```bash
cd /Users/amandacunha/Desktop/cobblers/cobblersBackend
k6 run -e ASSIGNMENT_ID=1 load-tests/submit-spike.js
```

To simulate a live room, create a session first and pass its code:

```bash
k6 run \
  -e ASSIGNMENT_ID=1 \
  -e SESSION_ID=ABCD \
  load-tests/submit-spike.js
```

## Find the threshold with a 1-65 ramp

To see exactly how latency grows as students join in:

```bash
k6 run \
  -e BASE_URL=https://cobblerscoders.tech \
  -e ASSIGNMENT_ID=1 \
  load-tests/submit-ramp-1-65.js
```

## Run the SignalR broadcast fan-out test

`submit-stress.js`/`submit-spike.js` only prove the REST submission path stays fast. Every submission also broadcasts `SubmissionRecorded` to everyone in the room over SignalR — with N students all submitting near-simultaneously, that's up to N×N small broadcast messages fanning out in a burst, and that path is untested by the REST-only scripts. `hub-broadcast-stress.js` closes that gap: each virtual user opens a real SignalR WebSocket to `/hub`, joins the room, submits, and measures how long its own broadcast takes to come back.

First create an active session and note its code:

```bash
curl -X POST http://localhost:5046/api/sessions \
  -H "Content-Type: application/json" \
  -d '{"assignmentSetId": "<your-set-id>"}'
```

Then run the test against that room:

```bash
k6 run \
  -e ASSIGNMENT_ID=1 \
  -e SESSION_ID=ABCD \
  -e VUS=80 \
  load-tests/hub-broadcast-stress.js
```

Point `BASE_URL` at whatever actually sits in front of `/hub` for the topology you care about — e.g. `https://cobblerscoders.tech`, which routes through the `frontend` (nginx) container's WebSocket proxy, the same path real students' browsers use. Hitting the backend directly (`:5046`) skips that proxy and only tells you about the backend/SignalR side.

Watch `own_broadcast_latency_ms` (time from submit to receiving your own broadcast) and `submission_broadcasts_received` (should approach `VUS` per connection if nothing is dropped under load) in the k6 summary.

## Test the deployed app

To check whether your VMs and DevOps setup can take the load, run k6 against the deployed backend.

### 1. Prefer a staging environment

Run the test against a staging deployment that mirrors production (same machine sizes, same Docker Compose, same Piston instance). Production is shared with real students; load testing it will create rows in the live database and may disrupt the workshop.

If you only have production, create a dedicated test assignment and run the test during a quiet window. Plan to clean up the student/submission rows afterwards.

### 2. Pick the right assignment

Use a `kind: 'code'` assignment on the deployed backend:

```bash
curl https://cobblerscoders.tech/api/assignmentsets
curl https://cobblerscoders.tech/api/assignmentsets/{setId}/assignments
```

Note the `id` of the assignment you want to hammer.

### 3. Run the spike against the deployment

```bash
k6 run \
  -e BASE_URL=https://cobblerscoders.tech \
  -e ASSIGNMENT_ID=1 \
  load-tests/submit-spike.js
```

If the target uses a non-standard port or you want to hit the backend directly:

```bash
k6 run \
  -e BASE_URL=https://cobblerscoders.tech:5046 \
  -e ASSIGNMENT_ID=1 \
  load-tests/submit-spike.js
```

### 4. Find the breaking point with a stress test

The spike script answers "can 100 students submit at once?" To find the limit of your machines, use the stress script instead. It ramps virtual users up, holds, then ramps down:

```bash
k6 run \
  -e BASE_URL=https://cobblerscoders.tech \
  -e ASSIGNMENT_ID=1 \
  -e MAX_VUS=100 \
  load-tests/submit-stress.js
```

Watch for the point where `http_req_duration` jumps or `http_req_failed` starts climbing. That is your practical ceiling.

### 5. Watch Grafana while the test runs

Open https://grafana.cobblerscoders.tech during the test. Correlate k6 output with:

- **Backend container**: CPU/memory (`container_cpu_usage_seconds_total`, `container_memory_usage_bytes`)
- **Postgres**: connection count, query latency, disk I/O
- **Piston**: queue depth and execution latency (if you have metrics exported from Piston)
- **VM**: CPU, memory, network I/O from node-exporter
- **cobblers_piston_request_duration_seconds** — custom metric emitted by the backend

Note the test start/end timestamps and zoom Grafana to that window. If response times spike while CPU is low, the bottleneck is likely Piston queuing or the Postgres connection pool. If CPU is pinned, the VM is undersized.

### 6. Make sure your load generator is not the bottleneck

k6 itself needs resources. 100 VUs is fine on a laptop. For hundreds or thousands of VUs:

- Run k6 on a cloud VM close to your deployment (low latency, high bandwidth).
- Increase open-file limits: `ulimit -n 10000`.
- Use k6 Cloud or a k6 Docker cluster for distributed load if one machine cannot generate enough traffic.

### 7. Save the results

k6 can write a JSON report for later analysis or for comparing before/after changes:

```bash
k6 run \
  -e BASE_URL=https://cobblerscoders.tech \
  -e ASSIGNMENT_ID=1 \
  --out json=submit-spike-results.json \
  load-tests/submit-spike.js
```

## Tune the load

### `submit-spike.js`

| Variable       | Default                  | Description                          |
| -------------- | ------------------------ | ------------------------------------ |
| `ASSIGNMENT_ID`| (required)               | Assignment to submit to              |
| `BASE_URL`     | `http://localhost:5046`  | Backend root URL                     |
| `SESSION_ID`   | (none)                   | Room code; omit for solo submissions |
| `VUS`          | `65`                     | Number of virtual users (students)   |
| `ITERATIONS`   | `1`                      | Submissions per student              |

Example: 65 students, 2 submits each:

```bash
k6 run -e ASSIGNMENT_ID=1 -e VUS=65 -e ITERATIONS=2 load-tests/submit-spike.js
```

### `submit-stress.js`

| Variable       | Default                  | Description                          |
| -------------- | ------------------------ | ------------------------------------ |
| `ASSIGNMENT_ID`| (required)               | Assignment to submit to              |
| `BASE_URL`     | `http://localhost:5046`  | Backend root URL                     |
| `SESSION_ID`   | (none)                   | Room code; omit for solo submissions |
| `MAX_VUS`      | `65`                     | Peak virtual users during the hold   |

Example: ramp to 100 virtual users to test headroom:

```bash
k6 run -e ASSIGNMENT_ID=1 -e MAX_VUS=100 load-tests/submit-stress.js
```

## Read the results

k6 prints a summary like:

```
http_req_duration..............: avg=2.31s   min=512ms  med=2.1s   max=8.4s   p(95)=5.2s
http_req_failed................: 0.00%  ✓ 0   ✗ 200
```

Key lines to watch:

- `http_req_failed` — should stay near 0%. If it climbs, the backend or Piston is dropping requests.
- `http_req_duration{tag:submit_assignment}` — end-to-end submit latency.
- `checks` — confirms every request returned the expected status/body.

## Interpreting failures

| Symptom                                   | Likely cause                                                     |
| ----------------------------------------- | ---------------------------------------------------------------- |
| `submit returned 200` fails               | Backend rejected the submission (400/404/500). Read the response. |
| High `http_req_duration` for submits      | Piston queue is backing up, or Postgres connection pool is small. |
| Timeouts or `http_req_failed`             | Backend or Piston crashed under load, or request timeout too low. |
| Register succeeds but submit fails with 400 | Assignment grader expects specific output; load code still runs.  |

The sample Java program intentionally prints arbitrary output, so `passed` may be `false` for graded assignments. That is fine for load testing — the goal is to verify the request path survives, not that the answer is correct.

## Safety

- **Do not run this against production** unless you intend to load-test production. It creates real student rows and real submission rows.
- Run against a local stack, a staging droplet, or a freshly-seeded database you can reset.
