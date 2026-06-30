using System.Text.Json.Serialization;


namespace cobblersBackend.Models;

public class Session
{
    [JsonPropertyName("code")]
    public string Code { get; set;} = string.Empty;

    public DateTime CreatedAt { get; set;}

    // TODO ObserveSession

    // TODO Timer Started

}