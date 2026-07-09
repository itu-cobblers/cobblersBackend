using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cobblersBackend.Data.Entities;

public class Attendance
{
    public required string StudentId { get; set; }
    public required string SessionId { get; set; }
    public required DateTimeOffset JoinedAt { get; set; }

    public required Student Student { get; set; }
    public required Session Session { get; set; }
    
}