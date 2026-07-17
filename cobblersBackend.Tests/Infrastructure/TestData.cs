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

    public static TaskSet MakeTaskSet(string? id = null, string? displayTitle = null)
    {
        id ??= $"taskset-{Next()}";
        return new TaskSet
        {
            TaskSetId = id,
            DisplayTitle = displayTitle ?? $"Title of {id}",
        };
    }

    public static Assignment MakeTask(TaskKind kind = TaskKind.Code, string? slug = null) => new()
    {
        Slug = slug ?? $"task-{Next()}",
        Kind = kind,
        Title = "Test task",
        Description = "A task for testing",
        ContentJson = """{"starter": ""}""",
        // SampleSolutionJson, GradingJson left null
    };

    public static TaskSetTask MakeTaskSetTask(string taskSetId, int taskId, int orderIndex) => new()
    {
        TaskSetId = taskSetId,
        TaskId = taskId,
        OrderIndex = orderIndex,
    };

    public static Session MakeSession(string taskSetId, string? code = null) => new()
    {
        SessionId = Guid.NewGuid().ToString(),
        Code = code ?? $"CODE{Next()}",
        TaskSetId = taskSetId,
        // CreateAt: not set because DB owns the parameter
    };

    public static Submission MakeSubmission(string studentId, int taskId, string? SessionId = null) => new()
    {
        SubId = Guid.NewGuid(),
        StudentId = studentId,
        TaskId = taskId,
        SessionId = SessionId,
        ContentJson = """{"code": "class Main {}"}""",
        // ResultJson, Passed left null
        // Submitted at owned by DB, never set
    };

}