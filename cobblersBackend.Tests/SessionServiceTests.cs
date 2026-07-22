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

}