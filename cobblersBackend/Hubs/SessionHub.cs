using cobblersBackend.Models;
using cobblersBackend.Services;
using Microsoft.AspNetCore.SignalR;

namespace cobblersBackend.Hubs;

/// <summary>
/// The room hub (CONTRACT.md, Sessions). A room is a SignalR Group named by the
/// session code. Students and observing teachers both join the group; broadcasts
/// are scoped to it, so solo students (never in a group) get nothing.
/// </summary>
public class SessionHub : Hub
{
    private readonly SessionStore _store;
    private readonly IAttendanceService _attendanceService;
    public SessionHub(SessionStore store, IAttendanceService attendanceService)  
    {
        _store = store;
        _attendanceService = attendanceService;
    }

    /// <summary>Student joins a room. Replies with current state; tells observers.</summary>
    public async Task<SessionState> JoinSession(JoinArgs args)
    {
        var code = SessionCode.Normalize(args.Code);

        // Persistence is authoritative: an unknown code must fail the join
        // loudly, *before* any live room state is touched — no half-joined
        // ghosts in the roster.
        try
        {
            await _attendanceService.RecordAttendanceAsync(code, args.StudentId);
        }
        catch (InvalidOperationException ex)
        {
            // HubException messages are sent to the caller even when detailed
            // errors are disabled — the client sees *why* the join failed.
            throw new HubException(ex.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, code);

        // Remember who/where this connection is, so OnDisconnected can clean up.
        // Stored normalized, so every later read matches the store/group keys.
        Context.Items["code"] = code;
        Context.Items["studentId"] = args.StudentId;

        var student = new StudentDto(args.StudentId, args.DisplayName);
        var roster = _store.AddStudent(code, student);

        // Tell observers (teacher) live: the one who joined, then the full list.
        await Clients.Group(code).SendAsync("StudentJoined", student);
        await Clients.Group(code).SendAsync("RosterUpdated", roster);

        return new SessionState(_store.GetTimer(code));
    }

    /// <summary>Teacher observes a room. Returns the current roster to the caller.</summary>
    public async Task<IReadOnlyList<StudentDto>> ObserveSession(string code)
    {
        code = SessionCode.Normalize(code);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return _store.GetRoster(code);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items["code"] is string code && Context.Items["studentId"] is string studentId)
        {
            var roster = _store.RemoveStudent(code, studentId);
            if (roster is not null)
                await Clients.Group(code).SendAsync("RosterUpdated", roster);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
