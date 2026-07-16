namespace cobblersBackend.Data.Entities;

public class Submission
{
    public required Guid SubId { get; set; }
    public required string StudentId { get; set; }
    public required int TaskId { get; set; }
    public string? SessionId { get; set; }
    public required string ContentJson { get; set; }
    public string? ResultJson { get; set; }
    public bool? Passed { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }

    public Student Student { get; set; } = null!;
    public Assignment Task { get; set; } = null!;
    public Session? Session { get; set; }
}