using cobblersBackend.Data.Entities;
using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/assignments")]
public class SubmissionController : ControllerBase
{
    private readonly ISubmissionService _service;

    public SubmissionController(ISubmissionService service) => _service = service;

    [HttpPost("{assignmentId}/submissions")]
    public async Task<IActionResult> Submit(int assignmentId, [FromBody] SubmissionRequestDto request)
    {

        try
        {
            var result = await _service.SubmitAsync(assignmentId,request);
            return result is null
                ? NotFound(new { error = $"Assignment '{assignmentId}' not found."})
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message});
        }
    }
}