using System.Text.Json.Serialization;

namespace cobblersBackend.Models;

// Wire contract with the frontend — see the api repo's CONTRACT.md (Sessions / Timer).
// [JsonPropertyName] makes the casing deterministic for both MVC (controllers)
// and SignalR (hub), regardless of the default naming policy.

/// <summary>A live room member, keyed by studentId so duplicates merge.</summary>
public record StudentDto(
    [property: JsonPropertyName("studentId")] string StudentId,
    [property: JsonPropertyName("displayName")] string DisplayName);

/// <summary>The countdown a teacher started, as an absolute end time.</summary>
public record TimerInfo(
    [property: JsonPropertyName("endsAt")] string EndsAt);

/// <summary>Replied to a student on join so a late joiner / reconnect syncs.</summary>
public record SessionState(
    [property: JsonPropertyName("activeTimer")] TimerInfo? ActiveTimer);

/// <summary>Args a student sends with JoinSession over the hub.</summary>
public record JoinArgs(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("studentId")] string StudentId,
    [property: JsonPropertyName("displayName")] string DisplayName);

// REST DTOs

// POST /api/sessions
public record CreateSessionResponse(
    [property: JsonPropertyName("code")] string Code);
public record CreateSessionRequest(
    [property: JsonPropertyName("tasksetId")] string TasksetId);

// GET /api/sessions/{code}
public record SessionDto(
    [property: JsonPropertyName("code")] string Code, 
    [property: JsonPropertyName("tasksetId")] string TasksetId);

// POST /api/sessions/{code}/timer
public record StartTimerRequest(
    [property: JsonPropertyName("durationMinutes")] int DurationMinutes);
