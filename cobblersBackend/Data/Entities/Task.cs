using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace cobblersBackend.Data.Entities;


public enum TaskKind
    {
        Code,
        Predict,
        Project
    }
public class Task
{
    [Key]
    public required int Id { get; set; }
    public required TaskKind Kind { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string? Hint { get; set; }
    public required string ContentJson { get; set; }
    public string? SampleSolutionJson { get; set; }

    public ICollection<TaskSetTask> TaskSets { get; set; } = [];
    public ICollection<Submission> Submissions { get; set; } = [];
    
}
