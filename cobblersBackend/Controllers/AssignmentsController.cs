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
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Executor is unreachable." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Executor did not respond in time." });
        }
    }

    [HttpGet("{assignmentId}/solution")]
    public async Task<IActionResult> GetSolution(int assignmentId)
    {
        var result = await _service.GetSolutionAsync(assignmentId);
        return result is null
            ? NotFound(new { error = $"Assignment '{assignmentId}' not found." })
            : Ok(result);
    }
}