using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;

namespace cobblersBackend.Tests;

[Collection("db")]
public class AssignmentServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private CobblersDbContext _db = null!;
    private AssignmentService _service = null!;

    public AssignmentServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
        _service = TestServices.Assignments(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private Assignment AddAssignment(
        string slug,
        string contentJson = """{"starter": "public class Main {}"}""",
        string? sampleSolutionJson = "\"the secret answer\"")
    {
        var assignment = new Assignment
        {
            Slug = slug,
            Kind = AssignmentKind.Code,
            Title = $"Title of {slug}",
            Description = "desc",
            Hint = null,
            ContentJson = contentJson,
            SampleSolutionJson = sampleSolutionJson,
            GradingJson = """{"op": "nonEmptyStdout"}""",
        };
        _db.Assignment.Add(assignment);
        _db.SaveChanges();
        return assignment;
    }

    [Fact]
    public async Task GetAssignmentsByIds_PreservesRequestOrder_NotDbOrder()
    {
        var a = AddAssignment("a");
        var b = AddAssignment("b");
        var c = AddAssignment("c");

        var result = await _service.GetAssignmentsByIdsAsync([c.Id, a.Id, b.Id]);

        Assert.Equal(
            new[] { "Title of c", "Title of a", "Title of b" },
            result.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task GetAssignmentsByIds_SkipsUnknownIds_Silently()
    {
        var known = AddAssignment("known");

        var result = await _service.GetAssignmentsByIdsAsync([999_999, known.Id, 888_888]);

        Assert.Equal(known.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetAssignmentsByIds_EmptyIds_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetAssignmentsByIdsAsync([]));
    }

    [Fact]
    public async Task GetAssignmentsByIds_Default_OmitsSolution()
    {
        var assignment = AddAssignment("with-sol");

        var dto = Assert.Single(await _service.GetAssignmentsByIdsAsync([assignment.Id]));

        Assert.Null(dto.Solution);
    }

    [Fact]
    public async Task GetAssignmentsByIds_IncludeSolution_AttachesParsedJson()
    {
        var stringSol = AddAssignment("string-sol", sampleSolutionJson: "\"public class Main {}\"");
        var fileList = AddAssignment(
            "file-list",
            sampleSolutionJson: """[{"name":"Main.java","content":"class Main {}"}]""");

        var result = await _service.GetAssignmentsByIdsAsync(
            [stringSol.Id, fileList.Id], includeSolution: true);

        Assert.Equal(2, result.Count);
        Assert.Equal("public class Main {}", result[0].Solution!.Value.GetString());
        Assert.Equal(JsonValueKind.Array, result[1].Solution!.Value.ValueKind);
        Assert.Equal("Main.java", result[1].Solution!.Value[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetAssignmentsByIds_IncludeSolution_NullSample_OmitsSolution()
    {
        var assignment = AddAssignment("no-sol", sampleSolutionJson: null);

        var dto = Assert.Single(
            await _service.GetAssignmentsByIdsAsync([assignment.Id], includeSolution: true));

        Assert.Null(dto.Solution);
    }

    [Fact]
    public async Task GetAssignmentsByIds_ContentIsParsedJson_AndKindIsLowercase()
    {
        var assignment = AddAssignment(
            "with-content",
            contentJson: """{"starter": "code here", "stdin": "50\n"}""");

        var dto = Assert.Single(await _service.GetAssignmentsByIdsAsync([assignment.Id]));

        Assert.Equal("code", dto.Kind);
        Assert.Equal("code here", dto.Content.GetProperty("starter").GetString());
        Assert.Equal("50\n", dto.Content.GetProperty("stdin").GetString());
    }

    [Fact]
    public async Task GetAssignmentsByIds_SerializedDto_LeaksNothingUnlessOptedIn()
    {
        var assignment = AddAssignment("sensitive");
        assignment.LessonJson = """[{"kind":"text","text":"hi"}]""";
        await _db.SaveChangesAsync();

        var appOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        var without = JsonSerializer.Serialize(
            await _service.GetAssignmentsByIdsAsync([assignment.Id]), appOptions);
        Assert.DoesNotContain("slug", without);
        Assert.DoesNotContain("secret answer", without);
        Assert.DoesNotContain("\"solution\"", without);
        Assert.DoesNotContain("nonEmptyStdout", without);

        var with = JsonSerializer.Serialize(
            await _service.GetAssignmentsByIdsAsync([assignment.Id], includeSolution: true), appOptions);
        Assert.Contains("\"solution\"", with);
        Assert.Contains("secret answer", with);
        Assert.DoesNotContain("nonEmptyStdout", with); // grading still never leaves
        Assert.DoesNotContain("slug", with);
    }
}
