using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cobblersBackend.Data.Entities;

public class TaskSetTask
{
    public int Id { get; set; }

    public required string TaskSetId { get; set; }

    public required int TaskId { get; set; }

    public required TaskSet TaskSet { get; set; }
    public required Task Task { get; set; }
}