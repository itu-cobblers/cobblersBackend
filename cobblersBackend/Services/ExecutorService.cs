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

        var options = new JsonSerializerOptions { WriteIndented = true };
        string prettyJson = JsonSerializer.Serialize(response, options);
        Console.WriteLine($"Piston response:\n{prettyJson}");

        return _classifier.Classify(response);
    }
}