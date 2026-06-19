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

ASP.NET Core Web API (`net10.0`, nullable + implicit usings enabled) that acts as a thin proxy in front of a [Piston](https://github.com/engineer-man/piston) code-execution engine. The app accepts source code over HTTP, forwards it to Piston, and collapses Piston's multi-stage result into a single output string.

Request flow:

```
ExecutorController (api/execute, POST)
  -> ExecutorService.ExecuteAsync(javaSource)
       -> IPistonClient.ExecuteAsync(language, code)   [PistonClient -> POST api/v2/execute]
       <- PistonExecuteResponse { compile?, run }
  <- string: compile.stderr | run.stderr | run.stdout
```

Key layering decisions:

- **`ExecutorService`** ([Services/ExecutorService.cs](cobblersBackend/Services/ExecutorService.cs)) holds the result-precedence logic: a non-zero `compile.Code` returns the compile error, then a non-zero `run.Code` returns the runtime error, otherwise `run.Stdout`. This is the unit under test and is deliberately decoupled from HTTP via `IPistonClient`.
- **`IPistonClient` / `PistonClient`** ([Services/PistonClient.cs](cobblersBackend/Services/PistonClient.cs)) is the only component that talks HTTP to Piston. It's registered as a typed `HttpClient` in [Program.cs](cobblersBackend/Program.cs), with `BaseAddress` from config key `Piston:BaseUrl` (default `http://localhost:2000/`).
- **Models** mirror the Piston v2 API wire format via `[JsonPropertyName]` attributes ([Models/](cobblersBackend/Models/)). `PistonExecuteResponse.Compile` is nullable because interpreted languages have no compile stage.

## In-progress / gotchas

- **Java-only, single-class assumption.** `PistonClient` hardcodes the filename `Main.java`, so it only works for submissions shaped like `public class Main { ... }` (Java requires the filename to match the public class name). The `language` passed from `ExecutorService` is also hardcoded to `"java"`. See the comment in `PistonClient` before generalizing.
- **Piston URL is set via environment variable.** Do not hardcode the droplet IP in `appsettings.json`. Set it at runtime with `export Piston__BaseUrl=http://<ip>:2000/` (double underscore maps to the `Piston:BaseUrl` config key). The `appsettings.json` placeholder (`http://localhost:2000/`) is intentional — it documents the expected shape without leaking the real address.
- **`PistonClient` has no HTTP error handling.** `EnsureSuccessStatusCode()` will throw an unhandled `HttpRequestException` if Piston returns 4xx/5xx. Fine for now; catch and wrap before exposing to real users.

## Testing

xUnit + Moq. Tests mock `IPistonClient` and assert `ExecutorService`'s output-selection behavior — no live Piston needed. New service logic should be testable the same way (mock the client, assert the returned string).
