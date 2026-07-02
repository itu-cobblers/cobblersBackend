
namespace cobblersBackend.DTOs;

public record ExecuteRequestDto(string? Code, FileDto[]? Files, string? EntryClass, string? Stdin);

public record FileDto(string? Name, string? Content);
