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
    private readonly ISessionService _session;
    private readonly SessionStore _store;
    private readonly IHubContext<SessionHub> _hub;
    private readonly ISubmissionService _submissions;


    public SessionsController(SessionStore store, IHubContext<SessionHub> hub, ISessionService session, ISubmissionService submissions)
    {
        _store = store;
        _hub = hub;
        _session = session;
        _submissions = submissions;
    }

    /// <summary>POST /api/sessions — create a room, return its join code.</summary>
    [HttpPost]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession(
        [FromBody] CreateSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AssignmentSetId))
            return BadRequest(new { error = "assignmentSetId is required" });

        try
        {
            var code = await _session.CreateSessionAsync(request.AssignmentSetId);
            return Ok(new CreateSessionResponse(code));
        }
        catch (InvalidOperationException ex)   // unknown assignment set
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<GetSessionResponse>> GetSession(string code)
    {
        var session = await _session.GetSessionAsync(code);
        return session is null ? NotFound() : Ok(session); 
    }

    /// <summary>
    /// GET /api/sessions/today-latest — the entry screen's "join session (CODE)"
    /// shortcut: today's newest still-active room, so a student doesn't have to
    /// type a code. 404 when no such room exists.
    /// </summary>
    [HttpGet("today-latest")]
    public async Task<ActionResult<GetSessionResponse>> GetTodayLatestSession()
    {
        var session = await _session.GetTodayLatestActiveSessionAsync();
        return session is null ? NotFound() : Ok(session);
    }

    /// <summary>
    /// POST /api/sessions/{code}/end — the teacher's manual "Exit"/"End session"
    /// action. Marks the room ended in the DB and broadcasts SessionEnded so any
    /// still-connected students bounce back to the entry screen.
    /// </summary>
    [HttpPost("{code}/end")]
    public async Task<IActionResult> EndSession(string code)
    {
        code = SessionCode.Normalize(code);
        var ended = await _session.EndSessionAsync(code);
        if (!ended)
            return NotFound(new { error = $"Session '{code}' not found." });

        _store.RemoveRoom(code);
        await _hub.Clients.Group(code).SendAsync("SessionEnded");

        return NoContent();
    }

    /// <summary>
    /// POST /api/sessions/{code}/timer — compute the absolute end time, store it
    /// on the room, then broadcast TimerStarted to the group.
    /// </summary>
    [HttpPost("{code}/timer")]
    public async Task<IActionResult> StartTimer(string code, [FromBody] StartTimerRequest request)
    {
        code = SessionCode.Normalize(code);
        if (await _session.GetSessionAsync(code) is null)
            return NotFound(new { error = $"Session '{code}' not found." });

        var endsAt = DateTimeOffset.UtcNow.AddMinutes(request.DurationMinutes);
        var timer = new TimerInfo(endsAt.ToString("o")); // ISO 8601 / round-trip
        _store.SetTimer(code, timer);

        await _hub.Clients.Group(code).SendAsync("TimerStarted", timer);

        return Ok(timer);
    }

    [HttpGet("{code}/assignments/{assignmentId:int}/submissions")]
    public async Task<IActionResult> GetAssignmentHistory(string code, int assignmentId, [FromQuery] string? studentId)
    {
        var rows = await _submissions.GetAssignmentHistoryAsync(code, assignmentId, studentId);
        return rows is null ? NotFound() : Ok(rows);
    }
   
}
