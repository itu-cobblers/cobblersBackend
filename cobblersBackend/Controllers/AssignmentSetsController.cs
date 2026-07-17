using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

/// <summary>
/// Assignment content endpoints (CONTRACT.md Assignments). Serves both
/// cohorts: the teacher's assignment-set picker and the students' assignment-list fetch.
/// </summary>
[ApiController]
[Route("api/assignmentsets")]
public class AssignmentSetsController : ControllerBase
{
    private readonly IAssignmentSetService _assignmentSets;

    public AssignmentSetsController(IAssignmentSetService assignmentSets)
    {
        _assignmentSets = assignmentSets;
    }

    /// <summary>GET /api/assignmentsets — list available assignment sets for the teacher's picker.</summary>
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _assignmentSets.ListAssignmentSetsAsync());

    /// <summary>
    /// GET /api/assignmentsets/{assignmentSetId}/assignments — the set's assignments,
    /// sorted by their position in the set (the response array index is the position).
    /// </summary>
    [HttpGet("{assignmentSetId}/assignments")]
    public async Task<IActionResult> GetAssignments(string assignmentSetId)
    {
        var assignments = await _assignmentSets.GetAssignmentsAsync(assignmentSetId);
        if (assignments is null)
            return NotFound(new { error = $"Assignment set '{assignmentSetId}' not found." });

        return Ok(assignments);
    }
}
