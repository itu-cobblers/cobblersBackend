// Services/PistonClient.cs
using System.Diagnostics;
using System.Net.Http.Json;
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

    public async Task<PistonExecuteResponse> ExecuteAsync(string language, string code)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = new PistonExecuteRequest
        {
            Language = language,
            Files = new List<PistonFile>
            {
                // Hardcoded for now: Java requires the filename to match the
                // public class name, so this only works for submissions shaped
                // like `public class Main { ... }`. Fine for the hello-world
                // test; revisit if the contract ever needs a different class name.
                new() { Name = "Main.java", Content = code }
            }
        };

        try
        {
            var httpResponse = await _httpClient.PostAsJsonAsync("api/v2/execute", request);
            stopwatch.Stop();
            _metrics.ObservePistonDuration(
                httpResponse.IsSuccessStatusCode ? "success" : "http_error",
                stopwatch.Elapsed.TotalSeconds);

            httpResponse.EnsureSuccessStatusCode();

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