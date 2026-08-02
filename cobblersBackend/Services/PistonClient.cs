// Services/PistonClient.cs
using System.Diagnostics;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

public class PistonClient : IPistonClient
{
    private readonly HttpClient _httpClient;
    private readonly IExecutionMetrics _metrics;

    public PistonClient(HttpClient httpClient, IExecutionMetrics metrics)
    {
        _httpClient = httpClient;
        _metrics = metrics;
    }
    
    public async Task<PistonExecuteResponse> ExecuteAsync(string language, IReadOnlyList<PistonFile> files)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = new PistonExecuteRequest
        {
            Language = language,
            Files = files.ToList()
        };

        try
        {
            var httpResponse = await _httpClient.PostAsJsonAsync("api/v2/execute", request);
            stopwatch.Stop();

            httpResponse.EnsureSuccessStatusCode();
            _metrics.ObservePistonDuration("success",stopwatch.Elapsed.TotalSeconds);

            var result = await httpResponse.Content.ReadFromJsonAsync<PistonExecuteResponse>();
            return result ?? throw new InvalidOperationException("Piston returned an empty response body.");
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            _metrics.ObservePistonDuration("http_error", stopwatch.Elapsed.TotalSeconds);
            throw;
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            _metrics.ObservePistonDuration("timeout", stopwatch.Elapsed.TotalSeconds);
            throw;
        }
    }
}