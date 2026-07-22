using System.Text.Json.Serialization;

namespace cobblersBackend.DTOs;

public record UpsertStudentRequestDto([property: JsonPropertyName("displayName")] string DisplayName);