# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build the whole solution (API + tests)
dotnet build

# Run the API (Development profile, http://localhost:5046)
dotnet run --project cobblersBackend

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~ExecuteAsync_WhenCompileFails_ReturnCompileError"
```

The `https` launch profile also exists (https://localhost:7049); the API calls `UseHttpsRedirection()`, so plain-http requests are redirected.

## Architecture

ASP.NET Core Web API (`net10.0`, nullable + implicit usings enabled) that acts as a thin proxy in front of a [Piston](https://github.com/engineer-man/piston) code-execution engine. The app accepts source code over HTTP, forwards it to Piston, and classifies Piston's result into a `status` + `stdout` + `stderr` response.

Request flow:

```
ExecutorController (api/execute, POST)
  -> ExecutorService.ExecuteAsync(javaSource)
       -> IPistonClient.ExecuteAsync(language, code)   [PistonClient -> POST api/v2/execute]
       <- PistonExecuteResponse { compile?, run }
       -> IExecuteResultClassifier.Classify(response)
  <- ExecuteResponseDto { status, stdout, stderr }
```

Key layering decisions:

- **`ExecutorService`** ([Services/ExecutorService.cs](cobblersBackend/Services/ExecutorService.cs)) just wires `IPistonClient` to `IExecuteResultClassifier` (plus a debug console dump of the raw Piston response). It holds no classification logic itself.
- **`IExecuteResultClassifier` / `JavaExecuteResultClassifier`** ([Services/JavaExecuteResultClassifier.cs](cobblersBackend/Services/JavaExecuteResultClassifier.cs)) owns result-precedence: `run.Code == 0` → `SUCCESS`; otherwise it infers `COMPILE_ERROR` vs `RUNTIME_ERROR` by sniffing `run.Stderr` (`.java:` + `error:` → compile error, `Exception in thread` → runtime error, anything else non-zero → runtime error). **It intentionally ignores `PistonExecuteResponse.Compile`** — this Piston instance's Java runtime (15.0.2) collapses compile and run into a single stage, so `Compile` is always `null` and a `compile.Code` check would be dead code. Don't "fix" this back to checking `Compile` without first confirming the deployed Piston/Java version still collapses stages.
- **`IPistonClient` / `PistonClient`** ([Services/PistonClient.cs](cobblersBackend/Services/PistonClient.cs)) is the only component that talks HTTP to Piston. It's registered as a typed `HttpClient` in [Program.cs](cobblersBackend/Program.cs), with `BaseAddress` from config key `Piston:BaseUrl` (default `http://localhost:2000/`).
- **Models** mirror the Piston v2 API wire format via `[JsonPropertyName]` attributes ([Models/](cobblersBackend/Models/)). `PistonExecuteResponse.Compile` stays nullable in the model (other languages/Piston setups may populate it), even though this deployment never does.

## Persistence (EF Core + Postgres)

A database layer lives under [Data/](cobblersBackend/Data/): `CobblersDbContext`, seven
entities (`Student`, `Session`, `Attendance`, `AssignmentSet`, `AssignmentSetAssignment`,
`Assignment`, `Submission`), and per-entity `IEntityTypeConfiguration<T>` classes in
`Data/Configurations/`, applied via `ApplyConfigurationsFromAssembly`. Several migrations
are on `main`. **[SCHEMA.md](SCHEMA.md) is the source of truth**
for the model and the reasoning behind every column — read it before touching entities.

- **Provider:** PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`; connection string
  from config key `ConnectionStrings:DefaultConnection`.
- **Naming:** global `snake_case` (`EFCore.NamingConventions`) — all identifiers lowercase.
- **Value generation** (see SCHEMA.md "Value generation"): timestamps and int identity keys
  are DB-owned (`now()` / identity, not `required`); client/app-supplied keys are
  `ValueGeneratedNever` + `required` (`Student.Id`, `Session.SessionId`/`Code`,
  `Submission.SubId`). `required` is not a generator — never put it on a DB-generated column.
- **Sessions/Attendance are wired and persist.** `SessionService`/`AttendanceService` write
  through `CobblersDbContext`; `SessionsController` + `SessionHub` call them. `SessionStore`
  (in-memory) now holds only the **live** SignalR roster + active timer — a different, smaller
  concern than the persisted historical record. `Submission` has no controller yet (S2).
- **Task → Assignment rename, in two stages (see SCHEMA.md's Assignment section for the
  full history):** a 2026-07-16 CLR-only rename (entity `Task`→`Assignment`, DB/wire stayed
  "task") was **superseded 2026-07-17** by a full wire+DB sweep once CONTRACT.md unified on
  Assignment vocabulary end to end (`/api/assignmentsets`, `assignmentId`). Tables are now
  `assignment`/`assignment_set`/`assignment_set_assignment`; `ITaskGrader`/`TaskGrader` are
  now `IAssignmentGrader`/`AssignmentGrader`. The rename landed as two migrations
  (`RenameAssignmentPhysicalNames`, `RenameTaskSetTablesToAssignmentSet`) that only
  rename tables/columns/constraints — **no data loss**. Both needed one hand-edit each
  after `dotnet ef migrations add`: EF's autogenerated script didn't know a downstream FK
  (`task_set_task → task`, `session → task_set`) depended on the primary key being
  renamed, and tried to drop that PK while the FK still referenced it (Postgres error
  `2BP01`). Fix pattern if this recurs on a future rename: add a `DropForeignKey`
  immediately before the failing `DropPrimaryKey`, and the matching `AddForeignKey`
  (pointed at the new table/column names) near the other `AddForeignKey` calls later in
  `Up()` — mirror in `Down()`, reversed.

## In-progress / gotchas

- **Connection string is set via environment variable.** Like the Piston URL, the real
  Postgres connection string (host/user/password) is **not** committed. Set it at runtime
  with `export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."`
  (double underscore maps to `ConnectionStrings:DefaultConnection`). `appsettings.json`
  holds only a localhost placeholder — never the VM host or a real password.

- **Java-only, single-class assumption.** `PistonClient` hardcodes the filename `Main.java`, so it only works for submissions shaped like `public class Main { ... }` (Java requires the filename to match the public class name). The `language` passed from `ExecutorService` is also hardcoded to `"java"`. See the comment in `PistonClient` before generalizing.
- **Piston URL is set via environment variable.** Do not hardcode the droplet IP in `appsettings.json`. Set it at runtime with `export Piston__BaseUrl=http://<ip>:2000/` (double underscore maps to the `Piston:BaseUrl` config key). The `appsettings.json` placeholder (`http://localhost:2000/`) is intentional — it documents the expected shape without leaking the real address.
- **`PistonClient` has no HTTP error handling.** `EnsureSuccessStatusCode()` will throw an unhandled `HttpRequestException` if Piston returns 4xx/5xx. Fine for now; catch and wrap before exposing to real users.
- **Compile-error detection is a `run.Stderr` substring heuristic**, not a structured Piston field — see `JavaExecuteResultClassifier` above. If Piston/Java version changes and starts populating `Compile` again, this classifier should be revisited.

## Testing

xUnit + Moq for pure logic (no DB): `ExecutorServiceTests`, `AssignmentGraderTests`. Mock
`IPistonClient` and assert `JavaExecuteResultClassifier`'s (or `ExecutorService`'s
end-to-end) status classification — no live Piston needed.

**DB-backed tests use a real Postgres via Testcontainers** (`cobblersBackend.Tests/Infrastructure/`)
— not SQLite, not EF InMemory, so unique/check/FK constraints and jsonb behave exactly
like the droplet. Requires Docker running locally; first run pulls `postgres:18-alpine`
(pinned to match the droplet's major version).

- **`PostgresFixture`** (`IAsyncLifetime`): starts one container per test run, runs
  `Database.MigrateAsync()` once, sets up a `Respawner` (ignoring `__EFMigrationsHistory`)
  for per-test resets. `CreateContext()` always chains `.UseSnakeCaseNamingConvention()` —
  omit it and every query fails looking for a PascalCase column that doesn't exist.
- **`[Collection("db")]`** on every DB test class + `DbCollection : ICollectionFixture<PostgresFixture>`
  — one shared container, tests run serially (xUnit parallelizes test *classes* by default;
  two classes resetting one database mid-test would race).
- **`TestData`** (`Infrastructure/TestData.cs`): builder methods for the FK graph
  (`MakeStudent`, `MakeAssignmentSet`, `MakeAssignment`, `MakeAssignmentSetAssignment`,
  `MakeSession`, `MakeSubmission`). Never set DB-owned columns (timestamps, identity ids);
  always set app-supplied ones (`Submission.SubId` — a forgotten `Guid.NewGuid()` collides
  as `Guid.Empty` on the second such row).
- Arrange and assert through **separate `DbContext` instances** for anything proving a
  round-trip actually hit Postgres — reading back through the same context can be served
  from the change tracker without touching the database at all.
