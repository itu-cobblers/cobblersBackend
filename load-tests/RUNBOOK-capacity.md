# Capacity runbook — is VM-2 big enough for 80 students?

One question, answered by arithmetic:

```
cores needed on VM-2 = (jobs in the burst × CPU-seconds per job) / (acceptable wait × 0.85)
```

Everything is known except **CPU-seconds per job** (`C`). Measure `C`, read the table,
decide. Budget ~115 minutes including a rescale.

Set these once per shell:

```bash
export VM1=root@<vm-1-address>
export VM2=root@<vm-2-address>
export VM2_INTERNAL=http://<address VM-1 uses to reach VM-2>:2000
export APP=https://cobblerscoders.tech
```

Paste every command's output into a scratch file as you go. A measurement you didn't write
down did not happen.

---

## Block A · 15 min — Ground truth

Nothing after this is interpretable without these values.

```bash
ssh $VM2 'docker inspect $(docker ps -q --filter name=piston) > /root/piston-inspect-BACKUP.json; docker inspect $(docker ps -q --filter name=piston) --format "ENV={{json .Config.Env}}{{println}}MOUNTS={{json .Mounts}}{{println}}IMAGE={{.Image}}{{println}}RESTART={{json .HostConfig.RestartPolicy}}"'
```

Record `PISTON_MAX_CONCURRENT_JOBS`, `PISTON_RUN_TIMEOUT`, `PISTON_RUN_CPU_TIME`,
`PISTON_LIMIT_OVERRIDES`, the image digest, the restart policy, and **every mount**. Expect
the limits to be unset: stock concurrency ~64 (sitting right under your 80) and stock **3 s**
`run_timeout` — the 10 s Java override exists only in `cobblersBackend/docker-compose.yml`
for local dev. A 3 s budget has to cover module load *plus* compile *plus* execute, because
Piston's Java 15.0.2 runs in JEP 330 source mode (`mv $1 $1.java && java $1.java`, see
`ExecutorService.cs:46-52`). That is tight even unloaded.

The mounts matter most: the Java package lives under `/piston/packages`, and the backup file
is your rollback if the container ever has to be recreated.

```bash
ssh $VM1 'docker inspect cobblers-backend --format "{{json .Config.Env}}" | tr "," "\n" | grep -i piston'
```

**Confirm this points at the current VM-2.** The committed default in
`cobblersDevOps/docker-compose.yaml:15` is `http://164.92.244.173:2000/` — a DigitalOcean IP,
while the VMs are now Hetzner. It is overridden by the `PISTON_BASE_URL` secret, and
`${VAR:-default}` substitutes on *empty*, so an empty secret silently points production at a
dead box. There is already a commit "trigger deploy: pick up corrected PISTON_BASE_URL
secret" — this has bitten you once.

```bash
ssh $VM1 'docker exec cobblers-postgres psql -U cobblers_user -d cobblers -c "select count(*) from submission; select count(*) from student; show max_connections;"'
ssh $VM2 'nproc; free -m; docker stats --no-stream'
```

---

## Block C1 · 5 min — `C`, the number the verdict rests on ★

```bash
scp load-tests/cpu-per-job.sh $VM2:/root/
ssh $VM2 'N=30 bash /root/cpu-per-job.sh'
```

It fires 30 sequential jobs at Piston on localhost and reads the Piston container's own CPU
accounting counter before and after. Sequential on purpose: at concurrency 1 there is no
queueing, so the number is pure service cost. The script prints `C` and the required core
count for each scenario directly.

**If it reports killed jobs at concurrency 1**, stop and read the raw response it dumps: a
stock 3 s `run_timeout` that cannot fit an unloaded hello-world is a much more urgent finding
than capacity, and it changes what you do next.

### The verdict table

| measured `C` | burst 80, ≤30 s | burst 80, ≤60 s | burst 20 staggered, ≤30 s | steady @ ρ≤0.5 | verdict for 4 vCPU |
|---|---|---|---|---|---|
| 1.0 | 3.1 | 1.6 | 0.8 | 2.5 | ✅ enough |
| 1.5 | 4.7 | 2.4 | 1.2 | 3.8 | ⚠️ marginal — fine at 60 s |
| 2.0 | 6.3 | 3.1 | 1.6 | 5.0 | ❌ 8 vCPU |
| 2.5 | 7.8 | 3.9 | 2.0 | 6.3 | ❌ 8 vCPU |
| 3.0 | 9.4 | 4.7 | 2.4 | 7.6 | ❌ 16 vCPU, or stagger |

Verdict = **max** of the burst column you accept and the steady column.

**Note what the staggered column does.** `jobs in the burst` is a classroom-management
variable, not a hardware one. Calling Run by row instead of "everyone now" divides the burst
requirement by four and is worth more than a VM tier, for free. Once you stagger, the burst
constraint disappears and **steady state becomes the binding constraint** — that is the
column to size against.

---

## Block C2 · 10 min — The concurrency knee

```bash
scp -r load-tests $VM1:/root/
ssh $VM1 "cd /root/load-tests && PISTON_URL=$VM2_INTERNAL bash sweep.sh"
```

Run from VM-1, which already reaches VM-2:2000 — no firewall change, no network noise, and
the backend is idle so the generator contends with nothing under test.

Read the throughput column: it should rise with K then flatten. The flattening point is the
knee and the right value for `PISTON_MAX_CONCURRENT_JOBS`. **It also tells you whether an
upgrade will help at all**: if throughput flattens near `cores / C`, more cores convert
directly into throughput; if it flattens well below that, CPU is not the limit and a bigger
VM buys nothing.

Watch two more columns: the first K where `sigkill` goes non-zero is where the configured
`run_timeout` starts killing correct student code under load, and peak container RSS — at
~100 MB per JVM, Piston's stock ~64 concurrent jobs is ~6.4 GB against VM-2's 8 GB.

If time is short, `KS="1 4 8 16"` is enough to see the shape.

---

## Block C3 · 12 min — The 80-way burst, full path

```bash
ulimit -n 4096
cd load-tests && BASE_URL=$APP VUS=80 k6 run run-burst.js
```

From your laptop is fine: the quantity of interest is a 30–60 s queueing tail, so 30 ms of
RTT is noise. `POST /api/execute` writes **nothing** to the database, so this is safe against
production.

While it runs, in another terminal:

```bash
ssh $VM2 'docker stats --no-stream' ; ssh $VM1 'docker stats --no-stream'
```

Watch **both** VMs. If VM-1 pins too, VM-1 needs a tier as well — otherwise VM-2 is the only
thing to buy.

Read off the output:
- **`MAX`** — the last student. This is the product requirement, not p95.
- **`drain`** vs the prediction `80 × C / (cores × 0.85)`. Agreement means the model holds and
  the table is trustworthy.
- **`bodyOK`** must be ≥ 99.5%.
- **`504`** must be 0. Any 504 is the timeout inversion, not capacity: nginx gives up at its
  stock 60 s while the backend waits out HttpClient's 100 s default, so a student sees an
  error for a job that actually succeeded and the backend holds the Piston slot 40 s longer.

Then re-run once with `PAYLOAD=typical` to confirm hello-world isn't flattering the numbers.

---

## Block D · 15 min — Decide

- **≤ 4 cores needed** → CX33 is enough. Go to Block F.
- **> 4 cores needed** → rescale VM-2 one tier (4 → 8 vCPU) in the Hetzner console (shutdown,
  resize, boot — a few minutes, reversible after the camp), then re-run C3.
- **> 8 cores needed** → stagger the class instead. Two tiers of upgrade to survive a burst
  pattern you can eliminate by saying "first row, run now" is not a good trade.

If steal time on VM-2 was material during C1–C3, prefer a **CCX dedicated-vCPU** instance at
the same core count over more shared cores — CX is shared, and a noisy neighbour makes a
CPU-bound workload unpredictable:

```bash
ssh $VM2 "grep 'cpu ' /proc/stat | awk '{print \"steal%=\", \$9/(\$2+\$3+\$4+\$5+\$6+\$7+\$8+\$9)*100}'"
```

---

## Block E · 20 min — Re-verify, plus the one risk an upgrade does not fix

Re-run C3 on the new size and check `MAX` against your target.

Then the join storm — the highest-probability event of tomorrow. 80 students joining at 09:00
is a *certainty*, while a synchronised burst is only a possibility, and **it is not a CPU
problem, so upgrading does not touch it**. `AttendanceService` does 3 SELECTs plus a
`SaveChanges` per `JoinSession`, 80 of them concurrently, against an Npgsql pool that defaults
to 100 and a Postgres `max_connections` of 100 — zero headroom. There is no
`EnableRetryOnFailure`, so exhaustion surfaces as a `HubException`, and the client's
`withAutomaticReconnect()` turns that into a retry storm.

Cheapest check, no script: open the app in ~10 tabs while watching

```bash
ssh $VM1 'docker exec cobblers-postgres psql -U cobblers_user -d cobblers -c "select count(*), state from pg_stat_activity group by state;"'
```

If connections grow roughly linearly per join, the fix is a **one-line compose edit, no image
build**: append `;Maximum Pool Size=25;Timeout=15` to `ConnectionStrings__DefaultConnection`
in `cobblersDevOps/docker-compose.yaml:16`, then `docker compose up -d --no-deps backend`. A
2-core Postgres cannot usefully serve more than ~8–16 concurrent queries, so queueing in the
app's pool is strictly better than queueing inside Postgres.

---

## Block F · 15 min — Two free things

Both survive any VM size, both take minutes, and both are more likely to ruin tomorrow than
capacity is.

**1. Freeze `main` on both repos.** Both auto-deploy on push, and `docker compose up -d`
recreates the backend, which wipes the in-memory `SessionStore` — **every live room dies:
roster, timer, focused assignment**. One merge during a class does this.

**2. Add `restart: unless-stopped`** to every service in `cobblersDevOps/docker-compose.yaml`
(none has one — it was deleted in a past swarm revert and never restored) and to the hand-run
Piston container. Without it, a crash tomorrow stays down until someone SSHs in.

```bash
ssh $VM2 'docker update --restart unless-stopped $(docker ps -q --filter name=piston)'
```

`docker update` sets the restart policy on a **running** container without recreating it, so
this carries none of the risk of a `docker run` (no chance of losing the `/piston/packages`
mount).

### Emergency card for tomorrow

```bash
# backend/frontend/db down
ssh $VM1 'cd ~/cobblersDevOps && docker compose up -d'
# Piston down — code execution failing for everyone
ssh $VM2 'docker start $(docker ps -aq --filter name=piston)'
# is the backend saturated right now?
ssh $VM1 'curl -s localhost:5046/metrics | grep -E "http_requests_in_progress|cobblers_execute_requests_total"'
```

Also: prefer `predict` assignments for any whole-class synchronised moment —
`GradePredict` makes **zero** Piston calls. And don't demo an infinite loop to 80 people at
once; with a concurrency cap of 6, five spinning loops eat 83% of execution capacity.

---

## Pass / fail

| criterion | threshold |
|---|---|
| `C` (CPU-s per job) | measured, not assumed — this is the deliverable |
| burst `MAX` (80, or 20 if staggering) | ≤ 30 s, or ≤ 60 s if you accept it |
| `bodyOK` | ≥ 99.5% — a payload that works at K=1 must work at K=80 |
| `504` count | 0 |
| VM-2 peak memory | ≤ 80% |
| VM-1 CPU during burst | if it pins, VM-1 needs a tier too |

`bodyOK` is the row that matters most and the one nobody writes down. A beginner told their
correct code is wrong loses twenty minutes and a teacher's attention — worse than slowness.
It is also invisible to `http_req_failed`, because Piston answers **HTTP 200** for a run it
killed and `JavaExecuteResultClassifier.cs:29` maps that kill to `RUNTIME_ERROR`, the same
value a real exception produces. That is the entire reason these scripts assert on the
response body.

## Scripts

| file | where it runs | what it answers |
|---|---|---|
| `cpu-per-job.sh` | **VM-2** | `C` — CPU-seconds per job, and the required core count |
| `sweep.sh` → `piston-sweep.js` | **VM-1** | the concurrency knee; whether more cores will help |
| `run-burst.js` | **laptop** | does an 80-way burst hold up through the real path |
| `lib/payloads.js` | — | the three Java payloads; each prints a marker last so the body can be asserted |

The five older `submit-*.js` / `hub-*.js` scripts are not on tonight's path. They check HTTP
status rather than the response body, their thresholds are untagged (so their "p95" is really
a p90 of the slow call averaged with a fast one), and their closed-model `ramping-vus` with no
think time overstates real load by ~30× while self-throttling at the knee. Fixing them is a
post-camp job.
