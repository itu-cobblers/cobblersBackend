using cobblersBackend.Data.Entities;
using cobblersBackend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;


namespace cobblersBackend.Tests;

[Collection("db")]
public sealed class ConstraintTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly string UniqueViolationCode = "23505";
    private readonly string CheckViolationCode = "23514";
    private readonly string ForeignKeyViolationCode = "23503";
    private readonly string RestrictViolationCode = "23001";
    
    public ConstraintTests(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<PostgresException> AssertPostgresErrorAsync(
        Func<Task> action, string sqlState)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(action);
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(sqlState, pg.SqlState);
        return pg;
    }

    //unique index tests
    [Fact]
    public async Task DuplicateSessionCode_ThrowsUniqueViolation()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        await ctx.SaveChangesAsync();

        // When
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "DUPE"));
        await ctx.SaveChangesAsync();
    
        // Then
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "DUPE"));
        await AssertPostgresErrorAsync(() => ctx.SaveChangesAsync(), UniqueViolationCode);
    }

    [Fact]
    public async Task DuplicateAssignmentInSet_ThrowsUniqueViolation()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        var assignment = TestData.MakeAssignment();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Assignment.Add(assignment);
        await ctx.SaveChangesAsync();

        // When
        ctx.AssignmentSetAssignment.Add(TestData.MakeAssignmentSetAssignment(assignmentSet.AssignmentSetId, assignment.Id, 0));
        await ctx.SaveChangesAsync();

        // Then
        ctx.AssignmentSetAssignment.Add(TestData.MakeAssignmentSetAssignment(assignmentSet.AssignmentSetId, assignment.Id, 1)); // same pair different slot
        await AssertPostgresErrorAsync(() => ctx.SaveChangesAsync(), UniqueViolationCode);

    }

    [Fact]
    public async Task DuplicateOrderIndex_ThrowsUniqueViolation()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        var a = TestData.MakeAssignment();
        var b = TestData.MakeAssignment();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Assignment.AddRange(a,b);
        await ctx.SaveChangesAsync();

        // When
        ctx.AssignmentSetAssignment.Add(TestData.MakeAssignmentSetAssignment(assignmentSet.AssignmentSetId, a.Id, 0));
        await ctx.SaveChangesAsync();

        // Then
        ctx.AssignmentSetAssignment.Add(TestData.MakeAssignmentSetAssignment(assignmentSet.AssignmentSetId, b.Id, 0)); // different assignment, same slot
        await AssertPostgresErrorAsync(() => ctx.SaveChangesAsync(), UniqueViolationCode);
    }

    [Fact]
    public async Task InvalidKind_ThrowsCheckViolation()
    {
        // Given
        await using var ctx = _fixture.CreateContext();

        // When
        var ex = await Assert.ThrowsAsync<PostgresException>(() => ctx.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO assignment (slug, kind, title, description, content_json)
            Values ('bad-kind', 'bogus', 'x', 'x', '{{}}')
            """));

        // Then
        Assert.Equal(CheckViolationCode, ex.SqlState);
    }

    [Fact]
    public async Task SubmissionWithUnknownAssignment_ThrowsForeignKeyViolation()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var student = TestData.MakeStudent();
        ctx.Student.Add(student);
        await ctx.SaveChangesAsync();

        // When
        ctx.Submission.Add(TestData.MakeSubmission(student.Id, assignmentId: 999_999));

        // Then
        await AssertPostgresErrorAsync(() => ctx.SaveChangesAsync(), ForeignKeyViolationCode);
    }

    // Two JoinSession requests race past AttendanceService AnyAsync check

    [Fact]
    public async Task ConcurrentJoin_SecondInsertThrowsUniqueViolation()
    {
        // Given
        string studentId, sessionId;
        await using (var setup = _fixture.CreateContext())
        {
            var student = TestData.MakeStudent();
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.Student.Add(student);
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            studentId = student.Id; sessionId = session.SessionId;
        }

        // When
        // Request 1
        await using (var reqA = _fixture.CreateContext())
        {
            reqA.Attendance.Add(new Attendance { StudentId = studentId, SessionId = sessionId});
            await reqA.SaveChangesAsync();
        }

        // Then
        // Request 2 - races in after request 1 is already committed.
        await using var reqB = _fixture.CreateContext();
        reqB.Attendance.Add(new Attendance { StudentId = studentId, SessionId = sessionId});
        await AssertPostgresErrorAsync(() => reqB.SaveChangesAsync(), UniqueViolationCode);
    }

    [Fact]
    public async Task ForgottenSubId_SecondSubmissionCollides()
    {
        // Given
        string studentId; int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var student = TestData.MakeStudent();
            var assignment = TestData.MakeAssignment();
            setup.Student.Add(student);
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            studentId = student.Id; assignmentId = assignment.Id;
        }

        // When
        await using (var first = _fixture.CreateContext())
        {
            first.Submission.Add(new Submission { SubId = Guid.Empty, StudentId = studentId, AssignmentId = assignmentId, ContentJson = "{}"});
            await first.SaveChangesAsync();
        }

        await using var second = _fixture.CreateContext();
        second.Submission.Add(new Submission { SubId = Guid.Empty, StudentId = studentId, AssignmentId = assignmentId, ContentJson = "{}"});
        
        // Then
        await AssertPostgresErrorAsync(() => second.SaveChangesAsync(), UniqueViolationCode);
    }

    [Fact]
    public async Task DeletingStudentWithSubmissions_ThrowsFRestrictViolation()
    {
        // Given
        string studentId;
        await using (var write = _fixture.CreateContext())
        {
            var student = TestData.MakeStudent();
            var assignment = TestData.MakeAssignment();
            write.Student.Add(student);
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            write.Submission.Add(TestData.MakeSubmission(student.Id, assignment.Id));
            await write.SaveChangesAsync();
            studentId = student.Id;
        }
        // When
        await using var attempt = _fixture.CreateContext();
        attempt.Student.Remove(new Student { Id = studentId, DisplayName = ""}); // stub — never loaded, so the dependent is untracked
        
        // Then
        await AssertPostgresErrorAsync(() => attempt.SaveChangesAsync(), RestrictViolationCode);
    }

    [Fact]
    public async Task DeletingAssignmentWithSubmissions_ThrowsRestrictViolation()
    {
        // Given
        int assignmentId;
        await using (var write = _fixture.CreateContext())
        {
            var student = TestData.MakeStudent();
            var assignment = TestData.MakeAssignment();
            write.Student.Add(student);
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            write.Submission.Add(TestData.MakeSubmission(student.Id, assignment.Id));
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        // When
        await using var attempt = _fixture.CreateContext();
        attempt.Assignment.Remove(new Assignment { Slug = "Test", Kind = AssignmentKind.Code, Title = "Bogus", Description = "To Be Deleted", ContentJson = "{}", Id = assignmentId}); // stub — never loaded, so the dependent is untracked
        
        // Then
        await AssertPostgresErrorAsync(() => attempt.SaveChangesAsync(), RestrictViolationCode);
    }

    [Fact]
    public async Task DeletingAssignmentSetReferencedBySession_ThrowsRestrictViolation()
    {
        // Given
        string assignmentSetId;
        await using (var write = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            write.AssignmentSet.Add(assignmentSet);
            await write.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            write.Session.Add(session);
            await write.SaveChangesAsync();
            assignmentSetId = assignmentSet.AssignmentSetId;
        }

        // When
        await using var attempt = _fixture.CreateContext();
        attempt.AssignmentSet.Remove(new AssignmentSet {AssignmentSetId = assignmentSetId, DisplayTitle = ""});

        // Then
        await AssertPostgresErrorAsync(() => attempt.SaveChangesAsync(), RestrictViolationCode);

    }

    [Fact]
    public async Task DeletingUnreferencedAssignmentSet_CascadesAssignmentSetAssignmentRows()
    {
        // Given
        var assignmentSet = TestData.MakeAssignmentSet();
        await using (var write = _fixture.CreateContext())
        {
            var assignment1 = TestData.MakeAssignment();
            var assignment2 = TestData.MakeAssignment();
            write.Assignment.Add(assignment1);
            write.Assignment.Add(assignment2);
            write.AssignmentSet.Add(assignmentSet);
            await write.SaveChangesAsync();

            write.AssignmentSetAssignment.Add(TestData.MakeAssignmentSetAssignment(assignmentSetId: assignmentSet.AssignmentSetId, assignmentId: assignment1.Id, 0));
            write.AssignmentSetAssignment.Add(TestData.MakeAssignmentSetAssignment(assignmentSetId: assignmentSet.AssignmentSetId, assignmentId: assignment2.Id, 1));
            await write.SaveChangesAsync();
        }

        // When
        await using var attempt = _fixture.CreateContext();
        var toDelete = await attempt.AssignmentSet.SingleAsync(s => s.AssignmentSetId == assignmentSet.AssignmentSetId);
        attempt.AssignmentSet.Remove(toDelete);
        await attempt.SaveChangesAsync();

        // Then
        await using var read = _fixture.CreateContext();
        var remaining = await read.AssignmentSetAssignment
            .Where(m => m.AssignmentSetId == assignmentSet.AssignmentSetId)
            .ToListAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task Timestamps_AreStampedByDatabase_NotDefaultOrClientTime()
    {
        // Given 
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        await ctx.SaveChangesAsync();

        // When
        var session = TestData.MakeSession(assignmentSet.AssignmentSetId); //CreateAt deliberately never set
        ctx.Session.Add(session);
        await ctx.SaveChangesAsync();

        // Then
        await using var read = _fixture.CreateContext();
        var reloaded = await read.Session.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.NotEqual(default, reloaded.CreateAt);
        Assert.True(reloaded.CreateAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.True(reloaded.CreateAt <= DateTimeOffset.UtcNow.AddMinutes(1));
    }
}