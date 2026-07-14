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
}
