using cobblersBackend.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/execute")]
public class ExecutorController : ControllerBase
{
    [HttpPost]
    public IActionResult Execute([FromBody] PistonExecuteRequest request)
    {
        var output = RunExecutor
    }
}