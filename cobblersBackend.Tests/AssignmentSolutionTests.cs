using cobblersBackend.DTOs;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Hubs;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Tests;

/// <summary>
/// <c>GetSolutionAsync</c> (STORIES.md S8) — a passthrough of
/// <c>Assignment.SampleSolutionJson</c>. It hangs off ISubmissionService but touches no
/// Submission at all: the reveal gate is a frontend concern, so the backend hands the
/// reference answer to anyone who asks.
/// </summary>
[Collection("db")]
public sealed class AssignmentSolutionTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    public AssignmentSolutionTests(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetSolutionAsync_ReturnsStringSolution()
    {
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.SampleSolutionJson = JsonSerializer.Serialize("public class Main {}");
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx);
        var result = await service.GetSolutionAsync(assignmentId);

        Assert.NotNull(result);
        Assert.Equal(JsonValueKind.String, result!.Solution!.Value.ValueKind);
        Assert.Equal("public class Main {}", result.Solution.Value.GetString());
    }

    [Fact]
    public async Task GetSolutionAsync_ReturnsFileListSolution()
    {
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.SampleSolutionJson = """[{"name":"Main.java","content":"class Main {}"}]""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx);
        var result = await service.GetSolutionAsync(assignmentId);

        Assert.NotNull(result);
        Assert.Equal(JsonValueKind.Array, result!.Solution!.Value.ValueKind);
    }

    [Fact]
    public async Task GetSolutionAsync_NoSampleSolution_ReturnsNullSolution()
    {
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx);
        var result = await service.GetSolutionAsync(assignmentId);

        Assert.NotNull(result);
        Assert.Null(result!.Solution);
    }

    [Fact]
    public async Task GetSolutionAsync_UnknownAssignment_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx);
        var result = await service.GetSolutionAsync(999_999);

        Assert.Null(result);
    }
}
