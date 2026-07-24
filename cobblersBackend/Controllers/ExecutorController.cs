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
        //temporyary guard for multi file submissions. TBD.
        if (string.IsNullOrEmpty(request.Code))
            return BadRequest("Only single-file `code` requests are supported currently");
            
        var result = await _executor.ExecuteAsync(request.Code!);
        return Ok(result);
    }
}