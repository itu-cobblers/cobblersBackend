using System.Text.Json;
using System.Text.Json.Serialization;

namespace cobblersBackend.DTOs;

public record SubmissionRequestDto(
    [property: JsonPropertyName("studentId")] string StudentId, 
    [property: JsonPropertyName("sessionId")] string? SessionId, 
    [property: JsonPropertyName("content")]   JsonElement Content);

public record SubmissionResponseDto(
    [property: JsonPropertyName("subId")]       Guid SubId,
    [property: JsonPropertyName("passed")]      bool? Passed,
    [property: JsonPropertyName("result")]      ExecuteResponseDto? Result,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt
);

public record SubmissionHistoryDto(
    [property: JsonPropertyName("subId")]        Guid SubId,
    [property: JsonPropertyName("assignmentId")] int AssignmentId,
    [property: JsonPropertyName("sessionId")]    string? SessionId, // =Session.Code, null for solo
    [property: JsonPropertyName("passed")]       bool? Passed,
    [property: JsonPropertyName("submittedAt")]  DateTimeOffset SubmittedAt);

public record SubmissionDetailDto(
    [property: JsonPropertyName("subId")]        Guid SubId,
    [property: JsonPropertyName("studentId")]    string StudentId,
    [property: JsonPropertyName("assignmentId")] int AssignmentId,
    [property: JsonPropertyName("sessionId")]    string? SessionId,          // =Session.Code, null for solo
    [property: JsonPropertyName("content")]      JsonElement Content,        // raw ContentJson, string | {name,content}[]
    [property: JsonPropertyName("result")]       ExecuteResponseDto? Result, // null for predict
    [property: JsonPropertyName("passed")]       bool? Passed,
    [property: JsonPropertyName("submittedAt")]  DateTimeOffset SubmittedAt);

public record AssignmentSubmissionDto(
    [property: JsonPropertyName("subId")] Guid SubId,
    [property: JsonPropertyName("studentId")] string StudentId,
    [property: JsonPropertyName("assignmentId")] int AssignmentId,
    [property: JsonPropertyName("passed")] bool? Passed,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt);
    
/// <summary>
/// `GET /api/assignments/{assignmentId}/solution` — passthrough of
/// <see cref="Data.Entities.Assignment.SampleSolutionJson"/> (CONTRACT.md
/// "Solution"). Reveal gating lives in the frontend, not here.
/// </summary>
public record SolutionResponseDto(
    [property: JsonPropertyName("solution")] JsonElement? Solution);