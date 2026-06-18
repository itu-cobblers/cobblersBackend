
namespace cobblersBackend.Services;

public class ExecutorService
{
    private readonly IPistonClient _piston;
    public ExecutorService(IPistonClient piston) => _piston = piston;

    public async Task<string> ExecuteAsync(string javaSource)
    {
        var response = await _piston.ExecuteAsync("java", javaSource);

        // compile errors
        if (response.Compile is { Code: not 0})
            return response.Compile.Stderr;

        // runtime errors
        if (response.Run.Code is not 0)
            return response.Run.Stderr;

        // successful result
        return response.Run.Stdout;
    }
}