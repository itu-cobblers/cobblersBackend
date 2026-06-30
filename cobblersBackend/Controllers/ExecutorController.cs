using cobblersBackend.DTOs;
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
    public async Task<IActionResult> Execute([FromBody] ExecuteRequestDto request)
    {
        var result = await _executor.ExecuteAsync(request.Code!);
        return Ok(result);
    }
}