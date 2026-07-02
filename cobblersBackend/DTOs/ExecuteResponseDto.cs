
using System.Text.Json.Serialization;

namespace cobblersBackend.DTOs;

public record ExecuteResponseDto(ExecuteStatus Status, string Stdout, string Stderr);

[JsonConverter(typeof(JsonStringEnumConverter<ExecuteStatus>))]
public enum ExecuteStatus
{
    // Mapped to "success"
    Success, 
    // Mapped to "compile_error"
    CompileError, 
    // Mapped to "runtime_error"
    RuntimeError 
}