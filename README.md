# cobblersBackend

An ASP.NET Core (.NET 10) Web API for **bootIT**: it runs submitted Java code by proxying it to a [Piston](https://github.com/engineer-man/piston) code-execution engine, serves assignment sets from PostgreSQL, and hosts a SignalR hub for live teacher↔student rooms.

The frontend lives in the sibling repo [`cobblersFrontend`](../cobblersFrontend); its dev server proxies `/api/*` and `/hub` to this API on port **5046**.

> ⚠️ Early days — this project is under active development. Some pieces are incomplete.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **PostgreSQL** (local install or Docker) — the API **refuses to start** without a connection string
- A reachable [Piston](https://github.com/engineer-man/piston) instance — run locally via Docker (`docker-compose.yml`, `http://localhost:2000/`)
- `psql` (Postgres client) for the seed script

## First-time setup

Do these once after cloning, **in order** — the API throws at startup if step 2's env var is missing, and the frontend shows no assignments until step 4's seed has run.

### 1. Create a local Postgres database

Any Postgres works. Easiest is the repo's `docker-compose.yml`, which also brings up Piston
(see [One-shot local dev](#one-shot-local-dev) below):

```bash
docker compose up -d postgres
```

Or with Homebrew: `brew install postgresql@17 && brew services start postgresql@17`, then `createdb bootit`.

### 2. Set the required environment variables

Config comes from **environment variables**, not `appsettings.json` (which holds only placeholders — never commit real hosts or passwords). Note the **double underscore** (`__`) that maps to the `:` in config keys.

```bash
# Required — Postgres connection (adjust to your local DB from step 1)
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=bootit;Username=postgres;Password=postgres"

# Required for real code execution — where Piston lives.
# Omit it if you run Piston locally on the default http://localhost:2000/ (e.g. via docker-compose.yml)
export Piston__BaseUrl=http://localhost:2000/
```

Add both `export` lines to your shell profile (`~/.zshrc`) so every new terminal has them — forgetting `ConnectionStrings__DefaultConnection` is the most common "backend won't start" cause:

```
InvalidOperationException: ConnectionStrings__DefaultConnection not set
```

### 3. Apply the database schema (EF Core migrations)

```bash
dotnet tool restore
dotnet ef database update --project cobblersBackend
```

### 4. Seed the assignments

`psql` uses URL-style connection strings (not the .NET semicolon format from step 2), so spell it out — same host/db/user/password as step 2:

```bash
psql "postgresql://postgres:postgres@localhost:5432/bootit" -f scripts/seed-tasks.sql
```

The seed script is **idempotent** — safe to re-run any time (locally or against the shared VM database). Re-running updates content in place, rebuilds set memberships, and never overwrites `sample_solution_json` values authored directly in the database. See the header of [scripts/seed-tasks.sql](scripts/seed-tasks.sql) for details.

### 5. Run

```bash
dotnet build
dotnet run --project cobblersBackend    # http://localhost:5046
```

In Development (`ASPNETCORE_ENVIRONMENT=Development`, the default launch profile), the OpenAPI document is at `http://localhost:5046/openapi/v1.json`.

### 6. Verify

```bash
# Code execution (needs Piston reachable):
curl -X POST http://localhost:5046/api/execute \
  -H "Content-Type: application/json" \
  -d '{"code": "public class Main { public static void main(String[] args) { System.out.println(\"Hello, World!\"); } }"}'

# Assignment data (needs the DB migrated + seeded):
curl http://localhost:5046/api/assignmentsets
```

Then start the frontend (`npm run dev` in `../cobblersFrontend` — see its README) and click **Run**: real output in its terminal panel means the whole chain (frontend → API → Piston) works.

## One-shot local dev

`scripts/run-dev.sh` does steps 1–2 and 5 for you: it starts Postgres + Piston via
`docker-compose.yml`, exports `ConnectionStrings__DefaultConnection` and `Piston__BaseUrl`
pointing at those local containers, and runs the API. When you stop the API (Ctrl+C), it
stops (not removes) the containers too — data persists in the `bootit_postgres_data` volume,
so the next run picks up where you left off.

```bash
./scripts/run-dev.sh
```

You still need to run migrations (step 3) and seed (step 4) the first time, or after a schema change.

## Testing

```bash
dotnet test
```

No live Piston is needed — it's stubbed at every layer. **Docker must be running**, though:
the DB-backed and whole-app tests spin up a real Postgres via Testcontainers (first run pulls
`postgres:18-alpine`). See [CLAUDE.md](CLAUDE.md#testing) for the four test layers and their
gotchas.

## API

The full frontend↔backend contract lives in [CONTRACT.md](CONTRACT.md) (source of truth). Highlights:

- `POST /api/execute` — run source code, get `{ status, stdout, stderr }` back
- `GET /api/assignmentsets` / `GET /api/assignmentsets/:id/assignments` — assignment data
- `GET /api/assignments/:id/solution` — the reference answer (reveal gating lives in the frontend)
- `POST /api/sessions`, `GET /api/sessions/:code`, `POST /api/sessions/:code/timer`, `POST /api/sessions/:code/end` — live rooms + timer
- `GET /api/sessions/:code/attendance` / `GET /api/sessions/:code/submissions` — teacher-dashboard hydration (the ever-joined roll + every thin attempt in the room)
- `PUT /api/students/:id`, `GET /api/students/:id/submissions` — identity + own history
- `POST /api/assignments/:id/submissions`, `GET /api/submissions/:subId` — submit, then replay one attempt in full
- `/hub` — SignalR hub for room broadcasts
- `/metrics` — Prometheus scrape endpoint

Every `/api/sessions/:code/…` lookup treats an **ended** room as not-found (404), while the
student/history endpoints keep serving its rows.

## Troubleshooting

- **`ConnectionStrings__DefaultConnection not set` at startup** → step 2 wasn't done in this terminal; `export` the connection string (and put it in your shell profile).
- **API starts but `/api/assignmentsets` is empty / errors** → migrations or seed missing; redo steps 3–4.
- **`/api/execute` throws `HttpRequestException`** → Piston unreachable; check `Piston__BaseUrl` (droplet IP + port 2000) or your local Piston.
- **Frontend gets mock/rotating results instead of real output** → this API isn't running on port 5046 (the frontend's proxy target).
