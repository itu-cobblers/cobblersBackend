using cobblersBackend.Data.Entities;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Tests;

[Collection("db")]
public sealed class SessionServiceTest : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    public SessionServiceTest(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateSessionAsync_UnknownAssignmentSet_Throws()
    {
        await using var ctx = _fixture.CreateContext();
        var service = new SessionService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSessionAsync("nope"));
    }

    [Fact]
    public async Task CreateSessionAsync_Persists_WithGeneratedCode()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        await ctx.SaveChangesAsync();

        // When
        var service = new SessionService(ctx);
        var code = await service.CreateSessionAsync(assignmentSet.AssignmentSetId);

        // Then
        Assert.Equal(4, code.Length);
        await using var read = _fixture.CreateContext();
        var session = await read.Session.SingleAsync(s => s.Code == code);
        Assert.Equal(assignmentSet.AssignmentSetId, session.AssignmentSetId);
    }

    [Fact]
    public async Task CreateSessionAsync_RetriesOnCodeCollision()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD"));
        await ctx.SaveChangesAsync();

        // When
        var codes = new Queue<string>(["ABCD", "WXYZ"]);
        var service = new SessionService(ctx, () => codes.Dequeue());

        var result = await service.CreateSessionAsync(assignmentSet.AssignmentSetId);

        // Then 
        Assert.Equal("WXYZ", result);
        await using var read = _fixture.CreateContext();
        Assert.Equal(2, await read.Session.CountAsync(s => s.AssignmentSetId == assignmentSet.AssignmentSetId));

    }

    [Fact]
    public async Task CreateSessionAsync_GivesUpAfterMaxRetries()
    {
        // Given
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD"));
        await ctx.SaveChangesAsync();

        // When 
        var service = new SessionService(ctx, () => "ABCD");

        // Then
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateSessionAsync(assignmentSet.AssignmentSetId));

    }

    [Fact]
    public async Task GetSessionAsync_UnknownCode_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD"));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);
        var result = await service.GetSessionAsync(""); // empty string input

        Assert.Null(result);
    }


    [Fact]
    public async Task GetSessionAsync_NormalizesCase()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD"));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);
        var result = await service.GetSessionAsync("abcd"); // lowercase input

        Assert.NotNull(result);
        Assert.Equal("ABCD", result.Code);
    }

    [Fact]
    public async Task GetSessionAsync_EndedSession_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD", status: SessionStatus.Ended));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);
        var result = await service.GetSessionAsync("ABCD");

        Assert.Null(result);
    }

    [Fact]
    public async Task EndSessionAsync_UnknownCode_ReturnsFalse()
    {
        await using var ctx = _fixture.CreateContext();
        var service = new SessionService(ctx);

        Assert.False(await service.EndSessionAsync("NOPE"));
    }

    [Fact]
    public async Task EndSessionAsync_MarksSessionEnded()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD"));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);
        var result = await service.EndSessionAsync("abcd"); // lowercase input, normalized

        Assert.True(result);
        await using var read = _fixture.CreateContext();
        var session = await read.Session.SingleAsync(s => s.Code == "ABCD");
        Assert.Equal(SessionStatus.Ended, session.Status);
    }

    [Fact]
    public async Task GetTodayLatestActiveSessionAsync_NoActiveSessions_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD", status: SessionStatus.Ended));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);
        var result = await service.GetTodayLatestActiveSessionAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTodayLatestActiveSessionAsync_ReturnsMostRecentActiveSession()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "OLD1"));
        await ctx.SaveChangesAsync();
        // A second insert sorts later by CreateAt (DB-stamped `now()`).
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "NEW1"));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);
        var result = await service.GetTodayLatestActiveSessionAsync();

        Assert.NotNull(result);
        Assert.Equal("NEW1", result.Code);
    }

    [Fact]
    public async Task EndSessionAsync_AlreadyEnded_ReturnsFalse()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD", status: SessionStatus.Ended));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);

        // Ending an already-ended room is a 404, like every other lookup on that
        // code — EndSessionAsync filters `Status == Active` the same way
        // GetSessionAsync does. Without this test the filter can be dropped and
        // only an end-to-end run would notice (it was in fact missing until
        // apiSmoke.sh caught the 204).
        Assert.False(await service.EndSessionAsync("ABCD"));

        await using var read = _fixture.CreateContext();
        Assert.Equal(SessionStatus.Ended, (await read.Session.SingleAsync(s => s.Code == "ABCD")).Status);
    }

    [Fact]
    public async Task GetSessionAsync_NormalizesWhitespaceAndCase()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        ctx.Session.Add(TestData.MakeSession(assignmentSet.AssignmentSetId, code: "ABCD"));
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);

        // SessionCode.Normalize trims as well as upper-cases — a code pasted out
        // of a chat message arrives with whitespace more often than not.
        var result = await service.GetSessionAsync("  abcd ");

        Assert.NotNull(result);
        Assert.Equal("ABCD", result.Code);
    }

    [Fact]
    public async Task GetTodayLatestActiveSessionAsync_IgnoresYesterdaysRoom()
    {
        await using var ctx = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(assignmentSet);
        var stale = TestData.MakeSession(assignmentSet.AssignmentSetId, code: "OLD1");
        ctx.Session.Add(stale);
        await ctx.SaveChangesAsync();

        // CreateAt is DB-owned, so back-date it after the insert.
        stale.CreateAt = DateTimeOffset.UtcNow.AddDays(-1);
        await ctx.SaveChangesAsync();

        var service = new SessionService(ctx);
        var result = await service.GetTodayLatestActiveSessionAsync();

        // "today-latest" is the student entry screen's shortcut — an active room
        // left open overnight must not be offered as today's class.
        Assert.Null(result);
    }
}