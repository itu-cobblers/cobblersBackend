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

    private sealed class RoomState
    {
        public ConcurrentDictionary<string, StudentDto> Students { get; } = new();
        public TimerInfo? ActiveTimer { get; set; }

        public IReadOnlyList<StudentDto> Roster() => 
            Students.Values.ToList();
    }
}
