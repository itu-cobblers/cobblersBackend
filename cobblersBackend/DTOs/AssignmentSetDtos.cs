using System.Text.Json;
using System.Text.Json.Serialization;

namespace cobblersBackend.DTOs;

// CONTRACT.md's Assignments section uses camelCase, but the app-wide serializer
// policy is snake_case — so every property carries an explicit name.

public record AssignmentSetSummaryDto(
    [property: JsonPropertyName("assignmentSetId")] string AssignmentSetId,
    [property: JsonPropertyName("displayTitle")] string DisplayTitle);

/// <summary>
/// One assignment as served to students. Deliberately excludes the sample
/// solution, grading rules, and slug (see SCHEMA.md) — Content is the stored
/// jsonb passed through verbatim, so its camelCase keys survive the serializer policy.
/// </summary>
public record AssignmentDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("hint"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Hint,
    [property: JsonPropertyName("content")] JsonElement Content);
