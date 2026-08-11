namespace cobblersBackend.Services;

/// <summary>
/// The one place session-code normalization lives. Codes are stored and
/// broadcast uppercase; SignalR group names and DB lookups are both
/// case-sensitive, so every entry point (controller action, hub method,
/// service) must pass caller-supplied codes through here first — then use
/// the normalized value for *everything* (groups, store keys, queries,
/// broadcasts). A raw code should not survive past the top of a method.
/// </summary>
public static class SessionCode
{
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    /// <summary>
    /// The observers-only SignalR group for a room: teachers watching the dashboard,
    /// never students. Pass an already-normalized code.
    /// </summary>
    /// <remarks>
    /// Exists because three broadcasts — <c>StudentJoined</c>, <c>RosterUpdated</c> and
    /// <c>SubmissionRecorded</c> — are only ever handled by the teacher (see
    /// <c>observeSession</c> in the frontend's <c>sessionHub.ts</c>; <c>joinSession</c>
    /// registers none of them). Sending them to the whole room made every student
    /// receive and discard them, and the cost is quadratic in class size: a measured
    /// 80-student join storm shipped 5375 <c>RosterUpdated</c> frames totalling
    /// <b>20.34 MB</b> that exactly one connection could use. Scoping them here makes
    /// that N² into N.
    ///
    /// The suffix cannot collide with a real room: <see cref="Normalize"/> uppercases
    /// codes, and generated codes are alphanumeric, so no code contains ':'.
    ///
    /// Broadcasts that students DO handle — <c>HandsUpdated</c>, <c>TimerStarted</c>,
    /// <c>AssignmentFocused</c>, <c>SessionEnded</c> — must keep going to the room
    /// group. Observers are members of both groups, so they still receive those.
    /// </remarks>
    public static string ObserversGroup(string normalizedCode) => $"{normalizedCode}:observers";
}
