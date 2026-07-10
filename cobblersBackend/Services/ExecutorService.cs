using System.Text.Json;

using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public class ExecutorService
{
    private readonly IPistonClient _piston;
    private readonly IExecuteResultClassifier _classifier;
    public ExecutorService(IPistonClient piston, IExecuteResultClassifier classifier)
    {
        _piston = piston;
        _classifier = classifier;
    }

    public async Task<ExecuteResponseDto> ExecuteAsync(string javaSource)
    {
        var response = await _piston.ExecuteAsync("java", javaSource);

        return _classifier.Classify(response);
    }
}