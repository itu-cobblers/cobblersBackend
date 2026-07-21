using System.Globalization;
using cobblersBackend.Data.Entities;

namespace cobblersBackend.Tests.Infrastructure;

public static class TestData
{
    private static int _n;
    private static int Next() => Interlocked.Increment(ref _n);

    public static Student MakeStudent(string? id = null, string displayName = "Maria") => new()
    {
        Id = id ?? $"student-{Next()}",
        DisplayName = displayName,
    };

    public static AssignmentSet MakeAssignmentSet(string? id = null, string? displayTitle = null)
    {
        id ??= $"assignmentset-{Next()}";
        return new AssignmentSet
        {
            AssignmentSetId = id,
            DisplayTitle = displayTitle ?? $"Title of {id}",
        };
    }

    public static Assignment MakeAssignment(AssignmentKind kind = AssignmentKind.Code, string? slug = null) => new()
    {
        Slug = slug ?? $"assignment-{Next()}",
        Kind = kind,
        Title = "Test assignment",
        Description = "An assignment for testing",
        ContentJson = """{"starter": ""}""",
        // SampleSolutionJson, GradingJson left null
    };

    public static AssignmentSetAssignment MakeAssignmentSetAssignment(string assignmentSetId, int assignmentId, int orderIndex) => new()
    {
        AssignmentSetId = assignmentSetId,
        AssignmentId = assignmentId,
        OrderIndex = orderIndex,
    };

    public static Session MakeSession(string assignmentSetId, string? code = null) => new()
    {
        SessionId = Guid.NewGuid().ToString(),
        Code = code ?? $"CODE{Next()}",
        AssignmentSetId = assignmentSetId,
        // CreateAt: not set because DB owns the parameter
    };

    public static Submission MakeSubmission(string studentId, int assignmentId, string? SessionId = null) => new()
    {
        SubId = Guid.NewGuid(),
        StudentId = studentId,
        AssignmentId = assignmentId,
        SessionId = SessionId,
        ContentJson = """{"code": "class Main {}"}""",
        // ResultJson, Passed left null
        // Submitted at owned by DB, never set
    };

}
