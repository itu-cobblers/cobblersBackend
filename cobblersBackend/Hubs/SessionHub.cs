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
    public SessionHub(SessionStore store) => _store = store;

    /// <summary>Student joins a room. Replies with current state; tells observers.</summary>
    public async Task<SessionState> JoinSession(JoinArgs args)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, args.Code);

        // Remember who/where this connection is, so OnDisconnected can clean up.
        Context.Items["code"] = args.Code;
        Context.Items["studentId"] = args.StudentId;

        var student = new Student(args.StudentId, args.DisplayName);
        var roster = _store.AddStudent(args.Code, student);

        // Tell observers (teacher) live: the one who joined, then the full list.
        await Clients.Group(args.Code).SendAsync("StudentJoined", student);
        await Clients.Group(args.Code).SendAsync("RosterUpdated", roster);

        return new SessionState(_store.GetTimer(args.Code));
    }

    /// <summary>Teacher observes a room. Returns the current roster to the caller.</summary>
    public async Task<IReadOnlyList<Student>> ObserveSession(string code)
    {
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
