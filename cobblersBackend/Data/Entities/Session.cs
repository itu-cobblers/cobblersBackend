namespace cobblersBackend.Data.Entities;

public enum SessionStatus
{
    Active,
    Ended
}

public class Session
{

    public required string SessionId { get; set; }
    public required string Code { get; set; }
    public required string AssignmentSetId { get; set; }
    public DateTimeOffset CreateAt { get; set; }
    // Defaults to Active on insert (DB default); flipped to Ended by the
    // teacher's "End session" action (POST /api/sessions/{code}/end).
    public SessionStatus Status { get; set; }

    public AssignmentSet AssignmentSet { get; set; } = null!;

}
