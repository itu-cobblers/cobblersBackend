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
        // Scoped to the observers group, not the room: students never handle either
        // event, and at 80 students the roster fan-out is the single largest source
        // of hub traffic in the app. See SessionCode.ObserversGroup.
        var observers = SessionCode.ObserversGroup(code);
        await Clients.Group(observers).SendAsync("StudentJoined", student);
        await Clients.Group(observers).SendAsync("RosterUpdated", roster);

        return new SessionState(_store.GetTimer(code), _store.GetFocusedAssignment(code), _store.GetRaisedHands(code));
    }

    /// <summary>Teacher observes a room. Returns the current roster to the caller.</summary>
    public async Task<IReadOnlyList<StudentDto>> ObserveSession(string code)
    {
        code = SessionCode.Normalize(code);

        // Both groups, deliberately. The room group carries what the teacher shares
        // with students (HandsUpdated, SessionEnded); the observers group carries the
        // dashboard-only events students have no handler for.
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionCode.ObserversGroup(code));

        return _store.GetRoster(code);
    }

    /// <summary>Teacher moves to a different assignment. Broadcasts it to the room so every student can follow.</summary>
    public async Task FocusAssignment(string code, int assignmentId)
    {
        code = SessionCode.Normalize(code);
        _store.SetFocusedAssignment(code, assignmentId);
        await Clients.Group(code).SendAsync("AssignmentFocused", assignmentId);
    }

    /// <summary>Student raises their hand, or a teacher/the student themselves lowers one — broadcasts the ordered queue to the room so both sides stay in sync.</summary>
    public async Task RaiseHand(string code, string studentId)
    {
        code = SessionCode.Normalize(code);
        var order = _store.RaiseHand(code, studentId);
        await Clients.Group(code).SendAsync("HandsUpdated", order);
    }

    /// <summary>See <see cref="RaiseHand"/> — same broadcast, opposite direction.</summary>
    public async Task LowerHand(string code, string studentId)
    {
        code = SessionCode.Normalize(code);
        var order = _store.LowerHand(code, studentId);
        await Clients.Group(code).SendAsync("HandsUpdated", order);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items["code"] is string code && Context.Items["studentId"] is string studentId)
        {
            var hadRaisedHand = _store.GetRaisedHands(code).Contains(studentId);
            var roster = _store.RemoveStudent(code, studentId);
            if (roster is not null)
            {
                // Observers only — the roster is dashboard state (see ObserveSession).
                await Clients.Group(SessionCode.ObserversGroup(code))
                             .SendAsync("RosterUpdated", roster);

                // RemoveStudent already dropped this student's raised hand (if any) —
                // tell the room so a lingering entry doesn't wait for someone else's click.
                // Room-wide, unlike the roster above: students render the hand queue too.
                if (hadRaisedHand)
                    await Clients.Group(code).SendAsync("HandsUpdated", _store.GetRaisedHands(code));
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}
