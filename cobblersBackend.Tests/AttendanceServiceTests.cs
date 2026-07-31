using cobblersBackend.Data.Entities;
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
        Assert.NotNull(roster);
        Assert.Equal(["Maria", "Joe", "Valarie"], roster.Select(s => s.DisplayName).ToArray());

    }

    // ── The three answers /attendance has to keep apart ──────────────────────
    // "no such room" (404), "ended room" (404) and "nobody joined yet" (200 [])
    // all used to collapse into an empty list. CONTRACT.md
    // "GET /api/sessions/{code}/attendance".

    /// <summary>An assignment set + one session, committed. Returns the room code.</summary>
    private async Task<string> SeedSessionAsync(SessionStatus status = SessionStatus.Active)
    {
        await using var setup = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        setup.AssignmentSet.Add(assignmentSet);
        await setup.SaveChangesAsync();
        var session = TestData.MakeSession(assignmentSet.AssignmentSetId, status: status);
        setup.Session.Add(session);
        await setup.SaveChangesAsync();
        return session.Code;
    }

    [Fact]
    public async Task GetAttendanceAsync_UnknownCode_ReturnsNull()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var service = new AttendanceService(ctx);

        // When
        var roster = await service.GetAttendanceAsync("ZZZZZZ");

        // Then — null is the controller's 404 signal.
        Assert.Null(roster);
    }

    [Fact]
    public async Task GetAttendanceAsync_EndedSession_ReturnsNull()
    {
        // Given — a room with a real attendee, then ended.
        var code = await SeedSessionAsync(SessionStatus.Ended);
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent("student-1", "Maria"));
            await write.SaveChangesAsync();
            await new AttendanceService(write).RecordAttendanceAsync(code, "student-1");
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var roster = await new AttendanceService(ctx).GetAttendanceAsync(code);

        // Then — not-found, not an empty roll: there is nothing live to hydrate.
        Assert.Null(roster);
    }

    [Fact]
    public async Task GetAttendanceAsync_ActiveRoomNobodyJoined_ReturnsEmptyList()
    {
        // Given — the teacher opened the room and is staring at an empty dashboard.
        var code = await SeedSessionAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var roster = await new AttendanceService(ctx).GetAttendanceAsync(code);

        // Then — empty, NOT null. This is the case a bare `ToListAsync()` gets
        // right by accident and the two above get wrong.
        Assert.NotNull(roster);
        Assert.Empty(roster);
    }

    [Fact]
    public async Task GetAttendanceAsync_NormalizesCase()
    {
        // Given
        var code = await SeedSessionAsync();
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent("student-1", "Maria"));
            await write.SaveChangesAsync();
            await new AttendanceService(write).RecordAttendanceAsync(code, "student-1");
        }

        // When — a caller sends the code lowercase and padded.
        await using var ctx = _fixture.CreateContext();
        var roster = await new AttendanceService(ctx).GetAttendanceAsync($" {code.ToLowerInvariant()}  ");

        // Then — both the existence check and the roll query see the same key.
        Assert.NotNull(roster);
        Assert.Equal(["Maria"], roster.Select(s => s.DisplayName).ToArray());
    }

    [Fact]
    public async Task GetAttendanceAsync_OnlyReturnsThisRoomsAttendees()
    {
        // Given — two live rooms, one student each.
        var roomA = await SeedSessionAsync();
        var roomB = await SeedSessionAsync();
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent("student-a", "Maria"));
            write.Student.Add(TestData.MakeStudent("student-b", "Jonas"));
            await write.SaveChangesAsync();
            var service = new AttendanceService(write);
            await service.RecordAttendanceAsync(roomA, "student-a");
            await service.RecordAttendanceAsync(roomB, "student-b");
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var roster = await new AttendanceService(ctx).GetAttendanceAsync(roomA);

        // Then
        Assert.NotNull(roster);
        var attendee = Assert.Single(roster);
        Assert.Equal("student-a", attendee.StudentId);
        Assert.Equal("Maria", attendee.DisplayName);
    }

    [Fact]
    public async Task GetAttendanceAsync_RollIsAppendOnly_NoLeaveShrinksIt()
    {
        // Given — a student joins, then "leaves" the only way a student can:
        // the connection drops. Nothing in the service removes Attendance rows.
        var code = await SeedSessionAsync();
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent("student-1", "Maria"));
            await write.SaveChangesAsync();
            var service = new AttendanceService(write);
            await service.RecordAttendanceAsync(code, "student-1");
            // A rejoin after the break must not duplicate the row either.
            await service.RecordAttendanceAsync(code, "student-1");
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var roster = await new AttendanceService(ctx).GetAttendanceAsync(code);

        // Then — CONTRACT.md: Leave ≡ disconnect for presence, and neither
        // deletes the persisted row. This roll is the `totalNum` denominator,
        // so it must not shrink when someone shuts their laptop.
        Assert.NotNull(roster);
        Assert.Single(roster);
        Assert.Equal("Maria", roster[0].DisplayName);
    }
}