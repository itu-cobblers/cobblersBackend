namespace cobblersBackend.Data.Entities;

public class Session
{

    public required string SessionId { get; set; }
    public required string Code { get; set; }
    public required string AssignmentSetId { get; set; }
    public DateTimeOffset CreateAt { get; set; }

    public AssignmentSet AssignmentSet { get; set; } = null!;

}
