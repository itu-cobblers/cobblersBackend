using cobblersBackend.Hubs;
using cobblersBackend.Models;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace cobblersBackend.Controllers;

/// <summary>
/// Teacher-side REST for rooms and timers (CONTRACT.md). The timer trigger is a
/// plain request; SignalR is used only to fan the timer out to the room — so the
/// teacher side stays simple and testable.
/// </summary>
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly SessionStore _store;
    private readonly IHubContext<SessionHub> _hub;
    private readonly ITaskSetService _taskSets;

    public SessionsController(SessionStore store, IHubContext<SessionHub> hub, ITaskSetService taskSets)
    {
        _store = store;
        _hub = hub;
        _taskSets = taskSets;
    }

    /// <summary>
    /// POST /api/sessions — create a room running the picked taskset, return
    /// its join code. tasksetId comes from GET /api/tasksets (CONTRACT.md).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest request)
    {
        if (!await _taskSets.ExistsAsync(request.TasksetId))
            return BadRequest(new { error = $"Taskset '{request.TasksetId}' not found." });

        var code = _store.CreateSession(request.TasksetId);
        return Ok(new CreateSessionResponse(code));
    }

    /// <summary>
    /// GET /api/sessions/{code} — resolve a room's taskset, so a joined
    /// student can fetch content via GET /api/tasksets/{tasksetId}/tasks.
    /// </summary>
    [HttpGet("{code}")]
    public IActionResult Get(string code)
    {
        if (!_store.Exists(code))
            return NotFound(new { error = $"Session '{code}' not found." });

        return Ok(new GetSessionResponse(code, _store.GetTasksetId(code)));
    }

    /// <summary>
    /// POST /api/sessions/{code}/timer — compute the absolute end time, store it
    /// on the room, then broadcast TimerStarted to the group.
    /// </summary>
    [HttpPost("{code}/timer")]
    public async Task<IActionResult> StartTimer(string code, [FromBody] StartTimerRequest request)
    {
        if (!_store.Exists(code))
            return NotFound(new { error = $"Session '{code}' not found." });

        var endsAt = DateTimeOffset.UtcNow.AddMinutes(request.DurationMinutes);
        var timer = new TimerInfo(endsAt.ToString("o")); // ISO 8601 / round-trip
        _store.SetTimer(code, timer);

        await _hub.Clients.Group(code).SendAsync("TimerStarted", timer);

        return Ok(timer);
    }
}
