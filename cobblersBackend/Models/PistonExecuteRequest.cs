using System.Text.Json.Serialization;

namespace cobblersBackend.Models;

public class PistonExecuteRequest
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "*";

    [JsonPropertyName("files")]
    public List<PistonFile> Files { get; set; } = new();

}

public class PistonFile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}