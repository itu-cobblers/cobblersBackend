namespace cobblersBackend.Data.Entities;

public class AssignmentSetAssignment
{
    public int Id { get; set; }
    public required string AssignmentSetId { get; set; }
    public required int AssignmentId { get; set; }

    /// <summary>0-based position of this assignment within the assignment set.<summary>
    public required int OrderIndex { get; set; }

    public AssignmentSet AssignmentSet { get; set; } = null!;
    public Assignment Assignment { get; set; } = null!;
}
