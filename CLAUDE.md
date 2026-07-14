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
entities (`Student`, `Session`, `Attendance`, `TaskSet`, `TaskSetTask`, `Task`,
`Submission`), and per-entity `IEntityTypeConfiguration<T>` classes in
`Data/Configurations/`, applied via `ApplyConfigurationsFromAssembly`. A single
`InitialCreate` migration is on `main`. **[SCHEMA.md](SCHEMA.md) is the source of truth**
for the model and the reasoning behind every column — read it before touching entities.

- **Provider:** PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`; connection string
  from config key `ConnectionStrings:DefaultConnection`.
- **Naming:** global `snake_case` (`EFCore.NamingConventions`) — all identifiers lowercase.
- **Value generation** (see SCHEMA.md "Value generation"): timestamps and int identity keys
  are DB-owned (`now()` / identity, not `required`); client/app-supplied keys are
  `ValueGeneratedNever` + `required` (`Student.Id`, `Session.SessionId`/`Code`,
  `Submission.SubId`). `required` is not a generator — never put it on a DB-generated column.
- **Not wired to controllers yet.** Session creation still runs through the in-memory
  `SessionStore`; the DB-backed write paths (persisting `Session`/`Attendance`/`Submission`)
  are groundwork-in-progress, not live endpoints.
- **`Task` entity vs `System.Threading.Tasks.Task`.** The CLR type collides with the
  framework `Task`; a planned rename to `Assignment` is deferred to its own commit. Until
  then, fully-qualify (`Entities.Task`) where async code and the entity meet.

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

xUnit + Moq. Tests should mock `IPistonClient` and assert `JavaExecuteResultClassifier`'s (or `ExecutorService`'s end-to-end) status classification — no live Piston needed. New service logic should be testable the same way (mock the client, assert the returned status/stdout/stderr).
