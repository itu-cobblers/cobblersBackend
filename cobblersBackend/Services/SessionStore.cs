using System.Collections.Concurrent;
using System.Security.Cryptography;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

/// <summary>
/// In-memory store of live rooms (sessions). Ephemeral by design — if the
/// server restarts the teacher re-creates the room (see CONTRACT.md). Registered
/// as a singleton; all members are thread-safe.
/// </summary>
public class SessionStore
{
    // Charset for room codes: uppercase, no ambiguous 0/O or 1/I (CONTRACT.md).
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 4;

    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    /// <summary>Create a room (running the given taskset) with a unique code among active sessions.</summary>
    public string CreateSession(string tasksetId)
    {
        string code;
        do
        {
            code = GenerateCode();
        } while (!_sessions.TryAdd(code, new Session(code) { TasksetId = tasksetId }));
        return code;
    }

    public bool Exists(string code) => _sessions.ContainsKey(code);

    /// <summary>
    /// The taskset a room runs. Null when the room is unknown — or when the
    /// room was implicitly created by a hub join/timer (GetOrAdd) rather than
    /// the teacher's POST; check Exists(code) first to tell those apart.
    /// </summary>
    public string? GetTasksetId(string code) =>
        _sessions.TryGetValue(code, out var session) ? session.TasksetId : null;

    /// <summary>Add a student to a room. Returns the full roster after the add.</summary>
    public IReadOnlyList<Student> AddStudent(string code, Student student)
    {
        var session = _sessions.GetOrAdd(code, c => new Session(c));
        session.Students[student.StudentId] = student;
        return session.Roster();
    }

    /// <summary>Remove a student from a room. Returns the roster, or null if the room is gone.</summary>
    public IReadOnlyList<Student>? RemoveStudent(string code, string studentId)
    {
        if (!_sessions.TryGetValue(code, out var session)) return null;
        session.Students.TryRemove(studentId, out _);
        return session.Roster();
    }

    public IReadOnlyList<Student> GetRoster(string code) =>
        _sessions.TryGetValue(code, out var session) ? session.Roster() : Array.Empty<Student>();

    /// <summary>Store the active timer on a room so late joiners sync to it.</summary>
    public void SetTimer(string code, TimerInfo timer)
    {
        var session = _sessions.GetOrAdd(code, c => new Session(c));
        session.ActiveTimer = timer;
    }

    public TimerInfo? GetTimer(string code) =>
        _sessions.TryGetValue(code, out var session) ? session.ActiveTimer : null;

    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    private sealed class Session(string code)
    {
        public string Code { get; } = code;
        // Nullable: rooms materialized via GetOrAdd (hub join / timer on an
        // unknown code) have no taskset; teacher-created rooms always do.
        public string? TasksetId { get; init; }
        public ConcurrentDictionary<string, Student> Students { get; } = new();
        public TimerInfo? ActiveTimer { get; set; }

        public IReadOnlyList<Student> Roster() => Students.Values.ToList();
    }
}
