namespace cobblersBackend.Data.Entities;


public enum TaskKind
{
    Code,
    Predict,
    Project
}

public class Task
{
    public int Id { get; set; }
    // Stable natural key (kebab-case, e.g. "hello-world"). Identical across
    // databases while Id is DB-assigned — seed upserts and any per-task code
    // hooks key on this, never on Id. Internal only, not exposed on the API.
    public required string Slug { get; set; }
    public required TaskKind Kind { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string? Hint { get; set; }
    public required string ContentJson { get; set; }
    public string? SampleSolutionJson { get; set; }
    // Serializable grading rules (see SCHEMA.md "Grading rules are data").
    // Null = not auto-gradable (projects, NIM) or graded generically (predict).
    public string? GradingJson { get; set; }

    public ICollection<TaskSetTask> TaskSets { get; set; } = [];
    public ICollection<Submission> Submissions { get; set; } = [];
    
}
