namespace cobblersBackend.Data.Entities;

public class TaskSetTask
{
    public int Id { get; set; }
    public required string TaskSetId { get; set; }
    public required int TaskId { get; set; }

    public TaskSet TaskSet { get; set; } = null!;
    public Task Task { get; set; } = null!;
}