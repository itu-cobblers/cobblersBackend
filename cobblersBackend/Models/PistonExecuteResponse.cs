using System.Text.Json.Serialization;

namespace cobblersBackend.Models;

public class PistonExecuteResponse
{
    [JsonPropertyName("run")]
    public required PistonStage Run { get; set; }

    [JsonPropertyName("compile")]
    public PistonStage? Compile { get; set; }
}

//using record (immutable) instead of class (mutable) to ensure data integrity
public record PistonStage(
    [property: JsonPropertyName("stdout")] string Stdout,
    [property: JsonPropertyName("stderr")] string Stderr,
    [property: JsonPropertyName("output")] string Output,
    [property: JsonPropertyName("code")] int? Code, 
    [property: JsonPropertyName("signal")] string? Signal
);