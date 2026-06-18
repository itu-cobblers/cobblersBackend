# cobblersBackend

An ASP.NET Core (.NET 10) Web API that runs submitted source code by proxying it to a [Piston](https://github.com/engineer-man/piston) code-execution engine.

> ⚠️ Early days — this project was just started and is still under active development. Some pieces are incomplete.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A reachable [Piston](https://github.com/engineer-man/piston) instance (defaults to `http://localhost:2000/`)

## Running

```bash
# Restore + build
dotnet build

# Run the API (http://localhost:5046)
dotnet run --project cobblersBackend
```

The Piston base URL can be overridden via the `Piston:BaseUrl` config key (e.g. in `appsettings.json` or as an environment variable).

## Testing

```bash
dotnet test
```

## API

`POST /api/execute` — submit source code and get back the program output (or the compile/runtime error).
