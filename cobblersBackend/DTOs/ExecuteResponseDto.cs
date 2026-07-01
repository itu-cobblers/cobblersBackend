
using System.Text.Json.Serialization;

namespace cobblersBackend.DTOs;

public record ExecuteResponseDto(ExecuteStatus Status, string Stdout, string Stderr);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecuteStatus
{
    [JsonStringEnumMemberName("success")] SUCCESS,
    [JsonStringEnumMemberName("compile_error")] COMPILE_ERROR,
    [JsonStringEnumMemberName("runtime_error")] RUNTIME_ERROR
}