using cobblersBackend.Data.Entities;

namespace cobblersBackend.Tests.Infrastructure;

/// <summary>The ids a seeded room's tests assert against.</summary>
public sealed record RoomSeed(
    string Code, int AssignmentA, int AssignmentB, Guid Oldest, Guid Middle, Guid Newest);

/// <summary>
/// Multi-row scenarios that more than one test class needs. Shared because the same
/// room is read from two angles: the teacher's room-wide list, and the student/detail
/// reads proving an ended room's rows stay reachable.
/// </summary>
public static class RoomSeeder
{
    /// <summary>
    /// A room with two students and two assignments, holding three attempts —
    /// one failed, one passed, one ungraded.
    ///
    /// <c>SubmittedAt</c> is written explicitly: the column is DB-owned
    /// (<c>DEFAULT now()</c>), so three rows inserted in one SaveChanges would share a
    /// timestamp and make ordering assertions flaky.
    /// </summary>
    public static async Task<RoomSeed> WithAttemptsAsync(
        PostgresFixture fixture, SessionStatus status = SessionStatus.Active)
    {
        await using var setup = fixture.CreateContext();

        var assignmentSet = TestData.MakeAssignmentSet();
        setup.AssignmentSet.Add(assignmentSet);
        await setup.SaveChangesAsync();

        var session = TestData.MakeSession(assignmentSet.AssignmentSetId, status: status);
        setup.Session.Add(session);

        var assignmentA = TestData.MakeAssignment(AssignmentKind.Code);
        var assignmentB = TestData.MakeAssignment(AssignmentKind.Predict);
        setup.Assignment.AddRange(assignmentA, assignmentB);
        setup.Student.Add(TestData.MakeStudent("student-maria", "Maria"));
        setup.Student.Add(TestData.MakeStudent("student-jonas", "Jonas"));
        await setup.SaveChangesAsync();

        var oldest = TestData.MakeSubmission("student-maria", assignmentA.Id, session.SessionId);
        var middle = TestData.MakeSubmission("student-jonas", assignmentA.Id, session.SessionId);
        var newest = TestData.MakeSubmission("student-maria", assignmentB.Id, session.SessionId);
        oldest.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        middle.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        newest.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        oldest.Passed = false;
        middle.Passed = true;
        // newest.Passed stays null — an ungraded kind.
        setup.Submission.AddRange(oldest, middle, newest);
        await setup.SaveChangesAsync();

        return new RoomSeed(session.Code, assignmentA.Id, assignmentB.Id,
                            oldest.SubId, middle.SubId, newest.SubId);
    }
}
