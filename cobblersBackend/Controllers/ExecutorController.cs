using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/execute")]
public class ExecutorController : ControllerBase
{
    private readonly ExecutorService _executor;
    public ExecutorController(ExecutorService executor) => _executor = executor;

    [HttpPost]
    public async Task<IActionResult> Execute([FromBody] ExecuteRequest request)
    {
        var output = await _executor.ExecuteAsync(request.Code);
        return Ok(new ExecuteResponse(output));
    }
}

public record ExecuteRequest(string Code);
public record ExecuteResponse(string Output);