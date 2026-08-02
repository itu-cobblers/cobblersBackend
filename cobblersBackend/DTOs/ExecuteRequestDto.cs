using System.Text.Json.Serialization;

namespace cobblersBackend.DTOs;

public record ExecuteRequestDto(
    [property: JsonPropertyName("code")] string? Code, 
    [property: JsonPropertyName("files")] FileDto[]? Files, 
    [property: JsonPropertyName("entryClass")] string? EntryClass, 
    [property: JsonPropertyName("stdin")] string? Stdin
);

public record FileDto(
    [property: JsonPropertyName("name")] string? Name, 
    [property: JsonPropertyName("content")] string? Content
);