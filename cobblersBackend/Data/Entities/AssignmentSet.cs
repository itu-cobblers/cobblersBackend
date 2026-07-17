namespace cobblersBackend.Data.Entities;

public class AssignmentSet
{
    public required string AssignmentSetId { get; set; }
    public required string DisplayTitle { get; set; }

    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<AssignmentSetAssignment> Assignments { get; set; } = [];

}
