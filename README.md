# cobblersBackend

An ASP.NET Core (.NET 10) Web API that runs submitted source code by proxying it to a [Piston](https://github.com/engineer-man/piston) code-execution engine.

> ⚠️ Early days — this project was just started and is still under active development. Some pieces are incomplete.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A reachable [Piston](https://github.com/engineer-man/piston) instance — either local (defaults to `http://localhost:2000/`) or the shared dev droplet (see below)

## Running

```bash
# Restore + build
dotnet build

# Run the API (http://localhost:5046)
dotnet run --project cobblersBackend
```

When running in Development (i.e. `ASPNETCORE_ENVIRONMENT=Development`), the OpenAPI document is available at `http://localhost:5046/openapi/v1.json` (Swagger-compatible).

The Piston base URL can be overridden via the `Piston:BaseUrl` config key (e.g. in `appsettings.json` or as an environment variable).

## Remote Piston (shared dev droplet)

A DigitalOcean droplet runs Piston so you don't have to host it locally.

- **Host:** `164.92.244.173`
- **Piston API:** listening on port `2000` (`http://164.92.244.173:2000/`)
- **Access:** SSH — your public key must be added to the droplet first (ask the team).

Point the API at the droplet by overriding the config key. The env-var form of
`Piston:BaseUrl` uses a **double underscore** (`Piston__BaseUrl`):

```bash
export Piston__BaseUrl=http://164.92.244.173:2000/
dotnet run --project cobblersBackend
```

Then exercise the running API (port 5046) end-to-end:

```bash
curl -X POST http://localhost:5046/api/execute \
  -H "Content-Type: application/json" \
  -d '{"code": "public class Main { public static void main(String[] args) { System.out.println(\"Hello, World!\"); } }"}'
```

## Database setup & task seed

The API uses PostgreSQL (connection string via the `ConnectionStrings__DefaultConnection`
environment variable — `appsettings.json` only holds a placeholder template).

```bash
# 1. Apply the schema migrations
dotnet tool restore
dotnet ef database update --project cobblersBackend

# 2. Load the 35 BootIT tasks + tasksets
psql "$CONNECTION_STRING" -f scripts/seed-tasks.sql
```

The seed script is **idempotent** — safe to re-run any time (locally or against
the shared VM database). Re-running updates task content in place, rebuilds the
taskset memberships, and never overwrites `sample_solution_json` values that
were authored directly in the database. See the header of
[scripts/seed-tasks.sql](scripts/seed-tasks.sql) for details.

## Testing

```bash
dotnet test
```

## API

`POST /api/execute` — submit source code and get back the program output (or the compile/runtime error).
