
namespace cobblersBackend.DTOs;

public record ExecuteResponseDto(string Status, string Stdout, string Stderr);

public static class ExecuteStatus
{
    public const string Success = "success";
    public const string CompileError = "compile_error";
    public const string RuntimeError = "runtime_error";
}