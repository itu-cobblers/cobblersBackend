
using cobblersBackend.DTOs;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

public class ExecutorService
{
    private readonly IPistonClient _piston;
    public ExecutorService(IPistonClient piston) => _piston = piston;

    public async Task<ExecuteResponseDto> ExecuteAsync(string javaSource)
    {
        var response = await _piston.ExecuteAsync("java", javaSource);

        // compile errors
        if (response.Compile is { Code: not 0})
            return new ExecuteResponseDto(ExecuteStatus.COMPILE_ERROR, "", response.Compile.Stderr);

        // runtime errors
        if (response.Run.Code is not 0)
            return new ExecuteResponseDto(ExecuteStatus.RUNTIME_ERROR, response.Run.Stdout, response.Run.Stderr);

        // successful result
        return new ExecuteResponseDto(ExecuteStatus.SUCCESS, response.Run.Stdout, response.Run.Stderr);
    }
}