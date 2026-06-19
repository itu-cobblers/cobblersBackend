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

ASP.NET Core Web API (`net10.0`, nullable + implicit usings enabled) that acts as a thin proxy in front of a [Piston](https://github.com/engineer-man/piston) code-execution engine. Clients connect via SignalR (WebSocket), submit source code, and receive the result pushed back when execution completes.

Entry points:

- **SignalR hub** (primary): `ExecutorHub` at `/hubs/executor`. Client invokes `ExecuteCode(code)`, result arrives via `ReceiveResult(output)` push.
- **REST controller** (will be removed): `ExecutorController` at `POST /api/execute`. Kept alongside the hub until the hub is verified working end-to-end.

Request flow (hub):

```
ExecutorHub.ExecuteCode(code)             [/hubs/executor, SignalR]
  -> IExecutorService.ExecuteAsync(code)
       -> IPistonClient.ExecuteAsync("java", code)   [PistonClient -> POST api/v2/execute]
       <- PistonExecuteResponse { compile?, run }
  <- Clients.Caller.ReceiveResult(string)            [pushed back to caller]
```

Request flow (REST):

```
ExecutorController (api/execute, POST)
  -> IExecutorService.ExecuteAsync(code)
       -> IPistonClient.ExecuteAsync("java", code)   [PistonClient -> POST api/v2/execute]
       <- PistonExecuteResponse { compile?, run }
  <- string: compile.stderr | run.stderr | run.stdout
```

Key layering decisions:

- **`IExecutorService` / `ExecutorService`** ([Services/ExecutorService.cs](cobblersBackend/Services/ExecutorService.cs)) holds the result-precedence logic: a non-zero `compile.Code` returns the compile error, then a non-zero `run.Code` returns the runtime error, otherwise `run.Stdout`. Registered as `AddScoped<IExecutorService, ExecutorService>()`. Decoupled from both HTTP and SignalR — the hub is just the entry point.
- **`IExecutorClient`** ([Hubs/IExecutorClient.cs](cobblersBackend/Hubs/IExecutorClient.cs)) is the strongly-typed client interface (`Hub<IExecutorClient>`). Defines `ReceiveResult(string output)` — the method name the frontend listens for. The typed hub means the compiler catches any mismatch.
- **`IPistonClient` / `PistonClient`** ([Services/PistonClient.cs](cobblersBackend/Services/PistonClient.cs)) is the only component that talks HTTP to Piston. Registered as a typed `HttpClient` in [Program.cs](cobblersBackend/Program.cs), with `BaseAddress` from config key `Piston:BaseUrl` (default `http://localhost:2000/`).
- **Models** mirror the Piston v2 API wire format via `[JsonPropertyName]` attributes ([Models/](cobblersBackend/Models/)). `PistonExecuteResponse.Compile` is nullable because interpreted languages have no compile stage.

## SignalR client pattern

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/executor")
    .build();

connection.on("ReceiveResult", (output) => {
    console.log(output);
});

await connection.start();
await connection.invoke("ExecuteCode", code);
```

## In-progress / gotchas

- **Java-only, single-class assumption.** `PistonClient` hardcodes the filename `Main.java`, so it only works for submissions shaped like `public class Main { ... }` (Java requires the filename to match the public class name). The `language` passed from `ExecutorService` is also hardcoded to `"java"`. See the comment in `PistonClient` before generalizing.
- **Piston URL is set via environment variable.** Do not hardcode the droplet IP in `appsettings.json`. Set it at runtime with `export Piston__BaseUrl=http://<ip>:2000/` (double underscore maps to the `Piston:BaseUrl` config key). The `appsettings.json` placeholder (`http://localhost:2000/`) is intentional — it documents the expected shape without leaking the real address.
- **`PistonClient` has no HTTP error handling.** `EnsureSuccessStatusCode()` will throw an unhandled `HttpRequestException` if Piston returns 4xx/5xx. Fine for now; catch and wrap before exposing to real users.

## Testing

xUnit + Moq. Tests mock `IPistonClient` and assert `ExecutorService`'s output-selection behavior — no live Piston needed. New service logic should be testable the same way (mock the client, assert the returned string).
