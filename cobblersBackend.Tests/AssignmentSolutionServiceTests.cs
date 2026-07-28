using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;

namespace cobblersBackend.Tests;

/// <summary>
/// GetSolutionAsync is deliberately kind-agnostic and gate-free (see
/// AssignmentSolutionService) — these tests only cover "does a stored sample
/// solution exist," never submission history or kind branching.
/// </summary>
[Collection("db")]
public class AssignmentSolutionServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private CobblersDbContext _db = null!;
    private AssignmentSolutionService _service = null!;

    public AssignmentSolutionServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
        _service = new AssignmentSolutionService(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private Assignment AddAssignment(string slug, AssignmentKind kind, string? sampleSolutionJson)
    {
        var assignment = new Assignment
        {
            Slug = slug,
            Kind = kind,
            Title = $"Title of {slug}",
            Description = "desc",
            ContentJson = """{"starter": "public class Main {}"}""",
            SampleSolutionJson = sampleSolutionJson,
        };
        _db.Assignment.Add(assignment);
        _db.SaveChanges();
        return assignment;
    }

    [Fact]
    public async Task GetSolutionAsync_UnknownAssignment_ReturnsNull()
    {
        Assert.Null(await _service.GetSolutionAsync(999_999));
    }

    [Fact]
    public async Task GetSolutionAsync_NoSampleSolution_IsUnavailable_RegardlessOfKind()
    {
        var code = AddAssignment("no-solution-code", AssignmentKind.Code, null);
        var predict = AddAssignment("no-solution-predict", AssignmentKind.Predict, null);
        var project = AddAssignment("no-solution-project", AssignmentKind.Project, null);

        foreach (var assignment in new[] { code, predict, project })
        {
            var result = await _service.GetSolutionAsync(assignment.Id);
            Assert.NotNull(result);
            Assert.False(result!.Available);
            Assert.Null(result.Solution);
        }
    }

    [Fact]
    public async Task GetSolutionAsync_CodeSingleFileSolution_IsAvailable()
    {
        var assignment = AddAssignment("code-solution", AssignmentKind.Code, "\"public class Main {}\"");

        var result = await _service.GetSolutionAsync(assignment.Id);

        Assert.NotNull(result);
        Assert.True(result!.Available);
        Assert.Equal("public class Main {}", result.Solution!.Value.GetString());
    }

    [Fact]
    public async Task GetSolutionAsync_ProjectMultiFileSolution_IsAvailable_AndDoesNotCareAboutKind()
    {
        var assignment = AddAssignment(
            "project-solution",
            AssignmentKind.Project,
            """[{"name":"Main.java","content":"class Main {}"}]""");

        var result = await _service.GetSolutionAsync(assignment.Id);

        Assert.NotNull(result);
        Assert.True(result!.Available);
        var files = result.Solution!.Value;
        Assert.Equal("Main.java", files[0].GetProperty("name").GetString());
        Assert.Equal("class Main {}", files[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task GetSolutionAsync_PredictWithSolutionStored_IsAlsoAvailable_NoKindGate()
    {
        // Predict assignments don't normally get SampleSolutionJson populated
        // (ContentJson.expectedOutput already is the answer — see SCHEMA.md),
        // but the service doesn't special-case `Kind` at all, so if one ever
        // did have a stored value, it would still come back.
        var assignment = AddAssignment("predict-solution", AssignmentKind.Predict, "\"42\"");

        var result = await _service.GetSolutionAsync(assignment.Id);

        Assert.NotNull(result);
        Assert.True(result!.Available);
        Assert.Equal("42", result.Solution!.Value.GetString());
    }
}
