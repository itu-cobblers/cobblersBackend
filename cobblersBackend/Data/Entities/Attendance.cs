namespace cobblersBackend.Data.Entities;

public class Attendance
{
    public required string StudentId { get; set; }
    public required string SessionId { get; set; }
    public required DateTimeOffset JoinedAt { get; set; }

    public Student Student { get; set; } = null!;
    public Session Session { get; set; } = null!;
    
}