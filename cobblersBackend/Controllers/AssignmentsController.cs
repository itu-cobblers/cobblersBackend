using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly ISubmissionService _service;

    public AssignmentsController(ISubmissionService service) => _service = service;

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