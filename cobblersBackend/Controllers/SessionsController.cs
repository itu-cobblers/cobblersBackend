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


    public SessionsController(SessionStore store, IHubContext<SessionHub> hub, ISessionService session)
    {
        _store = store;
        _hub = hub;
        _session = session;
    }

    /// <summary>POST /api/sessions — create a room, return its join code.</summary>
    [HttpPost]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession(
        [FromBody] CreateSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskSetId))
            return BadRequest(new { error = "tasksetId is required" });

        try
        {
            var code = await _session.CreateSessionAsync(request.TaskSetId);
            return Ok(new CreateSessionResponse(code));
        }
        catch (InvalidOperationException ex)   // unknown taskset
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<SessionDto>> GetSession(string code)
    {
        var session = await _session.GetSessionAsync(code);
        return session is null ? NotFound() : Ok(session); 
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

   
}
