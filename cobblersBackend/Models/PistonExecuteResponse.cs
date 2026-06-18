using System.Text.Json.Serialization;

namespace cobblersBackend.Models;

public class PistonExecuteResponse
{
    [JsonPropertyName("run")]
    public PistonStage Run { get; set; } = new();

    [JsonPropertyName("compile")]
    public PistonStage? Compile { get; set; }
}

public class PistonStage
{
    [JsonPropertyName("stdout")]
    public string Stdout { get; set; } = string.Empty;

    [JsonPropertyName("stderr")]
    public string Stderr { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public int? Code { get; set; }

}