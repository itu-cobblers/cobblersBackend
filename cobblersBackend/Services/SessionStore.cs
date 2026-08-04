using System.Collections.Concurrent;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

/// <summary>
/// In-memory store of live rooms (sessions). Ephemeral by design — if the
/// server restarts the teacher re-creates the room (see CONTRACT.md). Registered
/// as a singleton; all members are thread-safe.
/// </summary>
public class SessionStore
{
    private readonly ConcurrentDictionary<string, RoomState> _rooms = new();

    /// <summary>Add a student to a room. Returns the full roster after the add.</summary>
    public IReadOnlyList<StudentDto> AddStudent(string code, StudentDto student)
    {
        var room = _rooms.GetOrAdd(code, _ => new RoomState());
        room.Students[student.StudentId] = student;
        return room.Roster();
    }

    /// <summary>Remove a student from a room. Returns the roster, or null if the room is gone.</summary>
    public IReadOnlyList<StudentDto>? RemoveStudent(string code, string studentId)
    {
        if (!_rooms.TryGetValue(code, out var room)) return null;
        room.Students.TryRemove(studentId, out _);
        room.RaisedHands.TryRemove(studentId, out _);
        return room.Roster();
    }

    public IReadOnlyList<StudentDto> GetRoster(string code) =>
        _rooms.TryGetValue(code, out var room) ? room.Roster() : Array.Empty<StudentDto>();

    /// <summary>Store the active timer on a room so late joiners sync to it.</summary>
    public void SetTimer(string code, TimerInfo timer)
    {
        var room = _rooms.GetOrAdd(code, _ => new RoomState());
        room.ActiveTimer = timer;
    }

    public TimerInfo? GetTimer(string code) =>
        _rooms.TryGetValue(code, out var session) ? session.ActiveTimer : null;

    /// <summary>Store which assignment the teacher last focused, so late joiners sync to it.</summary>
    public void SetFocusedAssignment(string code, int assignmentId)
    {
        var room = _rooms.GetOrAdd(code, _ => new RoomState());
        room.FocusedAssignmentId = assignmentId;
    }

    public int? GetFocusedAssignment(string code) =>
        _rooms.TryGetValue(code, out var session) ? session.FocusedAssignmentId : null;

    /// <summary>Student raises a hand. Idempotent — re-raising an already-raised hand keeps its original place in the queue. Returns the ordered queue after the change.</summary>
    public IReadOnlyList<string> RaiseHand(string code, string studentId)
    {
        var room = _rooms.GetOrAdd(code, _ => new RoomState());
        room.RaisedHands.TryAdd(studentId, DateTimeOffset.UtcNow);
        return room.RaisedHandOrder();
    }

    /// <summary>Lower a hand — invoked by the student themselves or by the teacher. Returns the ordered queue after the change.</summary>
    public IReadOnlyList<string> LowerHand(string code, string studentId)
    {
        if (!_rooms.TryGetValue(code, out var room)) return Array.Empty<string>();
        room.RaisedHands.TryRemove(studentId, out _);
        return room.RaisedHandOrder();
    }

    /// <summary>StudentIds with a raised hand, oldest-raised first — so late joiners sync to the current queue.</summary>
    public IReadOnlyList<string> GetRaisedHands(string code) =>
        _rooms.TryGetValue(code, out var room) ? room.RaisedHandOrder() : Array.Empty<string>();

    /// <summary>Drop all live state (roster/timer/focus/raised hands) for a room — called once a session ends.</summary>
    public void RemoveRoom(string code) => _rooms.TryRemove(code, out _);

    private sealed class RoomState
    {
        public ConcurrentDictionary<string, StudentDto> Students { get; } = new();
        public TimerInfo? ActiveTimer { get; set; }
        public int? FocusedAssignmentId { get; set; }
        public ConcurrentDictionary<string, DateTimeOffset> RaisedHands { get; } = new();

        public IReadOnlyList<StudentDto> Roster() =>
            Students.Values.ToList();

        public IReadOnlyList<string> RaisedHandOrder() =>
            RaisedHands.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
    }
}
