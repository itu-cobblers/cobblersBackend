using System.Text.Json;

using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public class ExecutorService
{
    private readonly IPistonClient _piston;
    public ExecutorService(IPistonClient piston) => _piston = piston;

    public async Task<ExecuteResponseDto> ExecuteAsync(string javaSource)
    {
        var response = await _piston.ExecuteAsync("java", javaSource);

        // Terminal output for debugging piston response
        var options = new JsonSerializerOptions { WriteIndented = true };
        string prettyJson = JsonSerializer.Serialize(response, options);
        Console.WriteLine($"Piston response:\n{prettyJson}");

        // compile errors
        if (response.Compile is { Code: not 0})
            return new ExecuteResponseDto(ExecuteStatus.CompileError, "", response.Compile.Stderr);

        // runtime errors
        if (response.Run.Code is not 0)
            return new ExecuteResponseDto(ExecuteStatus.RuntimeError, response.Run.Stdout, response.Run.Stderr);

        // successful result
        return new ExecuteResponseDto(ExecuteStatus.Success, response.Run.Stdout, "");
    }
}