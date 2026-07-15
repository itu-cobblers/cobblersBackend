using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cobblersBackend.Controllers;

/// <summary>
/// Task content endpoints (CONTRACT.md Tasks). Serves both cohorts: the
/// teacher's taskset picker and the students' task-list fetch.
/// </summary>
[ApiController]
[Route("api/tasksets")]
public class TaskSetsController : ControllerBase
{
    private readonly ITaskSetService _taskSets;

    public TaskSetsController(ITaskSetService taskSets)
    {
        _taskSets = taskSets;
    }

    /// <summary>GET /api/tasksets — list available tasksets for the teacher's picker.</summary>
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _taskSets.ListTaskSetsAsync());

    /// <summary>
    /// GET /api/tasksets/{tasksetId}/tasks — the set's tasks, sorted by their
    /// position in the set (the response array index is the task's position).
    /// </summary>
    [HttpGet("{tasksetId}/tasks")]
    public async Task<IActionResult> GetTasks(string tasksetId)
    {
        var tasks = await _taskSets.GetTasksAsync(tasksetId);
        if (tasks is null)
            return NotFound(new { error = $"Taskset '{tasksetId}' not found." });

        return Ok(tasks);
    }
}
