using System.Text.Json;
using System.Text.Json.Serialization;

namespace cobblersBackend.DTOs;

public record SubmissionRequestDto(
    [property: JsonPropertyName("studentId")] string StudentId, 
    [property: JsonPropertyName("sessionId")] string? SessionId, 
    [property: JsonPropertyName("content")] JsonElement Content);

public record SubmissionResponseDto(
    [property: JsonPropertyName("subId")] Guid SubId,
    [property: JsonPropertyName("passed")] bool? Passed,
    [property: JsonPropertyName("result")] ExecuteResponseDto? Result,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt
);