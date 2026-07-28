using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

/// <summary>
/// Per-assignment endpoints that aren't scoped to an assignment set (CONTRACT.md).
/// </summary>
[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentSolutionService _solutions;

    public AssignmentsController(IAssignmentSolutionService solutions)
    {
        _solutions = solutions;
    }

    /// <summary>
    /// GET /api/assignments/{assignmentId}/solution — reveal an assignment's
    /// sample solution. Deliberately generic (no `kind` branch) and gate-free
    /// (no `studentId`/timer checks) — see AssignmentSolutionService.
    /// </summary>
    [HttpGet("{assignmentId:int}/solution")]
    public async Task<IActionResult> GetSolution(int assignmentId)
    {
        var result = await _solutions.GetSolutionAsync(assignmentId);
        if (result is null)
            return NotFound(new { error = $"Assignment {assignmentId} not found." });

        return Ok(result);
    }
}
