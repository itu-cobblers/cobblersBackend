using System.Text.Json;
using System.Diagnostics;

using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public class ExecutorService
{
    private readonly IPistonClient _piston;
    private readonly IExecuteResultClassifier _classifier;
    private readonly IExecutionMetrics _metrics;

    public ExecutorService(
        IPistonClient piston,
        IExecuteResultClassifier classifier,
        IExecutionMetrics metrics)
    {
        _piston = piston;
        _classifier = classifier;
        _metrics = metrics;
    }

    public async Task<ExecuteResponseDto> ExecuteAsync(string javaSource)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await _piston.ExecuteAsync("java", javaSource);
        stopwatch.Stop();

        var classifiedResponse = _classifier.Classify(response);
        _metrics.ObserveExecutionResult(classifiedResponse.Status, stopwatch.Elapsed.TotalSeconds);

        return classifiedResponse;
    }
}