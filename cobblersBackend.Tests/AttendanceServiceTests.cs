using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Tests;

[Collection("db")]
public sealed class AttendanceServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    public AttendanceServiceTests(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RecordAttendanceAsync_UnknownCode_Throws()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var service = new AttendanceService(ctx);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAttendanceAsync("nope","wrong"));
    }

    [Fact]
    public async Task RecordAttendanceAsync_UnknownStudent_Throws()
    {
        // Given
        string sessionCode;
        await using (var setup = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            sessionCode = session.Code;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new AttendanceService(ctx);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAttendanceAsync(sessionCode,"student-1"));
    }

    [Fact]
    public async Task RecordAttendanceAsync_KnownStudent_CreatesAttendance()
    {
        // Given
        string sessionCode;
        await using (var setup = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            setup.Student.Add(TestData.MakeStudent("student-1", "Maria"));
            await setup.SaveChangesAsync();
            sessionCode = session.Code;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new AttendanceService(ctx);
        await service.RecordAttendanceAsync(sessionCode, "student-1");

        // Then
        Assert.Equal(1, await ctx.Attendance.CountAsync());

    }

    [Fact]
    public async Task RecordAttendanceAsync_Rejoin_IsIdempotent()
    {
        // Given
        string sessionCode;
        await using (var setup = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            setup.Student.Add(TestData.MakeStudent("student-1", "Maria"));
            await setup.SaveChangesAsync();
            sessionCode = session.Code;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new AttendanceService(ctx);
        await service.RecordAttendanceAsync(sessionCode, "student-1");
        await service.RecordAttendanceAsync(sessionCode, "student-1");

        // Then
        Assert.Equal(1, await ctx.Student.CountAsync());
        Assert.Equal(1, await ctx.Attendance.CountAsync());

    }

    [Fact]
    public async Task RecordAttendanceAsync_SameStudentTwoSessions_OneStudentTwoAttendanceRows()
    {
        // Given
        string sessionCode1;
        string sessionCode2;
        await using (var setup = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session1 = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session1);
            await setup.SaveChangesAsync();
            var session2 = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session2);
            await setup.SaveChangesAsync();
            setup.Student.Add(TestData.MakeStudent("student-1", "Maria"));
            await setup.SaveChangesAsync();
            sessionCode1 = session1.Code;
            sessionCode2 = session2.Code;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new AttendanceService(ctx);
        await service.RecordAttendanceAsync(sessionCode1, "student-1");
        await service.RecordAttendanceAsync(sessionCode2, "student-1");

        // Then
        Assert.Equal(1, await ctx.Student.CountAsync());
        Assert.Equal(2, await ctx.Attendance.CountAsync());

    }
    
    [Fact]
    public async Task GetAttendanceAsync_ReturnsInJoinOrder()
    {
        // Given
        string sessionCode;
        await using (var setup = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            setup.Student.Add(TestData.MakeStudent("student-1", "Maria"));
            setup.Student.Add(TestData.MakeStudent("student-2", "Joe"));
            setup.Student.Add(TestData.MakeStudent("student-3", "Valarie"));
            await setup.SaveChangesAsync();
            sessionCode = session.Code;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new AttendanceService(ctx);
        await service.RecordAttendanceAsync(sessionCode, "student-1");
        await service.RecordAttendanceAsync(sessionCode, "student-2");
        await service.RecordAttendanceAsync(sessionCode, "student-3");

        // Then
        var roster = await service.GetAttendanceAsync(sessionCode);
        Assert.Equal(["Maria", "Joe", "Valarie"], roster.Select(s => s.DisplayName).ToArray());

    }
}