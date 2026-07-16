namespace cobblersBackend.Data.Entities;

public class TaskSetTask
{
    public int Id { get; set; }
    public required string TaskSetId { get; set; }
    public required int TaskId { get; set; }

    /// <summary>0-based position of this task within the taskset.<summary>
    public required int OrderIndex { get; set; }

    public TaskSet TaskSet { get; set; } = null!;
    public Assignment Task { get; set; } = null!;
}