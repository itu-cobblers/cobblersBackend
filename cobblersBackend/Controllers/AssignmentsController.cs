using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(
        ISubmissionService submissionService, 
        IAssignmentService assignmentService)
    {
        _submissionService = submissionService;
        _assignmentService = assignmentService;
    }

    [HttpPost("{assignmentId}/submissions")]
    public async Task<IActionResult> Submit(int assignmentId, [FromBody] SubmissionRequestDto request)
    {
        try
        {
            var result = await _submissionService.SubmitAsync(assignmentId, request);
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
        var result = await _submissionService.GetSolutionAsync(assignmentId);
        return result is null
            ? NotFound(new { error = $"Assignment '{assignmentId}' not found." })
            : Ok(result);
    }
    
    // GET /api/assignments?ids=1&ids=2&ids=5&includeSolution=true
    [HttpGet]
    public async Task<IActionResult> GetAssignmentsByIds(
        [FromQuery] int[] ids, 
        [FromQuery] bool includeSolution = false)
    {
        if (ids is null || ids.Length == 0)
            return BadRequest(new { error = "No assignment IDs provided." });

        var assignments = await _assignmentService.GetAssignmentsByIdsAsync(ids, includeSolution);
    
        return Ok(assignments);
    }
}