using System.ComponentModel.DataAnnotations;

namespace cobblersBackend.Data.Entities;

public class TaskSet
{
    [Key]
    public required string TaskSetId { get; set; }
    public required string DisplayTitle { get; set; }

    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<TaskSetTask> Tasks { get; set; } = [];
    
}