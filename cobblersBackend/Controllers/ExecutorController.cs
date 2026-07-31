using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/execute")]
public class ExecutorController : ControllerBase
{
    private readonly IExecutorService _executor;
    public ExecutorController(IExecutorService executor) => _executor = executor;

    [HttpPost]
    public async Task<IActionResult> Execute([FromBody] ExecuteRequestDto request)
    {
        if (!string.IsNullOrEmpty(request.Code))
            return await RunAndRespondAsync(() => _executor.ExecuteAsync(request.Code!));

        if (request.Files is { Length: > 0 })
        {
            if (string.IsNullOrWhiteSpace(request.EntryClass))
                return BadRequest(new { error = "entryClass is required when files is given" });

            if (request.Files.Any(f => string.IsNullOrEmpty(f.Name) || f.Content is null))
                return BadRequest(new { error = "Each file requires a name and content" });

            var entryFileName = $"{request.EntryClass}.java";
            if (!request.Files.Any(f => string.Equals(f.Name, entryFileName, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { error = $"No file named '{entryFileName}' matches entryClass '{request.EntryClass}'" });

            return await RunAndRespondAsync(() => _executor.ExecuteAsync(request.Files, request.EntryClass!));
        }

        return BadRequest(new { error = "Provide either `code` or `files` + `entryClass`." });
    }

    // Piston/network infrastructure failures map to 502/503 (CONTRACT.md) — a
    // student's broken code is still 200 OK with a compile_error/runtime_error status.
    private static async Task<IActionResult> RunAndRespondAsync(Func<Task<ExecuteResponseDto>> run)
    {
        try
        {
            return new OkObjectResult(await run());
        }
        catch (HttpRequestException)
        {
            return new ObjectResult(new { error = "Executor is unreachable." })
                { StatusCode = StatusCodes.Status502BadGateway };
        }
        catch (TaskCanceledException)
        {
            return new ObjectResult(new { error = "Executor did not respond in time." })
                { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }
}