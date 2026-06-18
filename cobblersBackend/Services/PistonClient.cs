// Services/PistonClient.cs
using System.Net.Http.Json;
using cobblersBackend.Models;

namespace cobblersBackend.Services;

public class PistonClient : IPistonClient
{
    private readonly HttpClient _httpClient;

    public PistonClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PistonExecuteResponse> ExecuteAsync(string language, string code)
    {
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

        var httpResponse = await _httpClient.PostAsJsonAsync("api/v2/execute", request);
        httpResponse.EnsureSuccessStatusCode();

        var result = await httpResponse.Content.ReadFromJsonAsync<PistonExecuteResponse>();
        return result ?? throw new InvalidOperationException("Piston returned an empty response body.");
    }
}