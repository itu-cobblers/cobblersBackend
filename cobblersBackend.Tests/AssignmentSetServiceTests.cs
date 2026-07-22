using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;

namespace cobblersBackend.Tests;

[Collection("db")]
public class AssignmentSetServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private CobblersDbContext _db = null!;
    private AssignmentSetService _service = null!;

    public AssignmentSetServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
        _service = new AssignmentSetService(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private Assignment AddAssignment(string slug, string contentJson = """{"starter": "public class Main {}"}""")
    {
        var assignment = new Assignment
        {
            Slug = slug,
            Kind = AssignmentKind.Code,
            Title = $"Title of {slug}",
            Description = "desc",
            Hint = null,
            ContentJson = contentJson,
            SampleSolutionJson = "\"the secret answer\"",
            GradingJson = """{"op": "nonEmptyStdout"}""",
        };
        _db.Assignment.Add(assignment);
        return assignment;
    }

    private void AddSet(string assignmentSetId, params (Assignment assignment, int order)[] members)
    {
        _db.AssignmentSet.Add(new AssignmentSet { AssignmentSetId = assignmentSetId, DisplayTitle = $"Title of {assignmentSetId}" });
        _db.SaveChanges(); // assign assignment ids before wiring memberships
        foreach (var (assignment, order) in members)
            _db.AssignmentSetAssignment.Add(new AssignmentSetAssignment { AssignmentSetId = assignmentSetId, AssignmentId = assignment.Id, OrderIndex = order });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetAssignments_SortsByOrderIndex_NotByIdOrInsertOrder()
    {
        var third = AddAssignment("third");
        var first = AddAssignment("first");
        var second = AddAssignment("second");
        AddSet("day1", (third, 2), (first, 0), (second, 1));

        var assignments = await _service.GetAssignmentsAsync("day1");

        Assert.NotNull(assignments);
        Assert.Equal(
            new[] { "Title of first", "Title of second", "Title of third" },
            assignments.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task GetAssignments_AssignmentInTwoSets_AppearsInBoth()
    {
        var shared = AddAssignment("shared");
        AddSet("day1", (shared, 0));
        AddSet("solo", (shared, 0));

        var day1 = await _service.GetAssignmentsAsync("day1");
        var solo = await _service.GetAssignmentsAsync("solo");

        Assert.Equal(shared.Id, Assert.Single(day1!).Id);
        Assert.Equal(shared.Id, Assert.Single(solo!).Id);
    }

    [Fact]
    public async Task GetAssignments_UnknownAssignmentSet_ReturnsNull_ButEmptySetReturnsEmpty()
    {
        AddSet("empty-set");

        Assert.Null(await _service.GetAssignmentsAsync("nope"));
        Assert.Empty((await _service.GetAssignmentsAsync("empty-set"))!);
    }

    [Fact]
    public async Task GetAssignments_ContentIsParsedJson_AndKindIsLowercase()
    {
        var assignment = AddAssignment("with-content", """{"starter": "code here", "stdin": "50\n"}""");
        AddSet("day1", (assignment, 0));

        var dto = Assert.Single((await _service.GetAssignmentsAsync("day1"))!);

        Assert.Equal("code", dto.Kind);
        Assert.Equal("code here", dto.Content.GetProperty("starter").GetString());
        Assert.Equal("50\n", dto.Content.GetProperty("stdin").GetString());
    }

    [Fact]
    public async Task GetAssignments_LessonIsParsedJson_WhenPresent_AndOmittedWhenNull()
    {
        var withLesson = AddAssignment("with-lesson");
        withLesson.LessonJson = """[{"kind":"text","text":"Printing…"},{"kind":"code","code":"System.out.println(1);"}]""";
        var withoutLesson = AddAssignment("no-lesson");
        AddSet("day1", (withLesson, 0), (withoutLesson, 1));

        var assignments = await _service.GetAssignmentsAsync("day1");

        Assert.NotNull(assignments);
        Assert.Equal(2, assignments.Count);
        Assert.Equal("text", assignments[0].Lesson!.Value[0].GetProperty("kind").GetString());
        Assert.Equal("Printing…", assignments[0].Lesson!.Value[0].GetProperty("text").GetString());
        Assert.Equal("System.out.println(1);", assignments[0].Lesson!.Value[1].GetProperty("code").GetString());
        Assert.Null(assignments[1].Lesson);
    }

    [Fact]
    public async Task GetAssignments_SerializedDto_LeaksNothing_AndUsesCamelCase()
    {
        var assignment = AddAssignment("sensitive");
        assignment.LessonJson = """[{"kind":"text","text":"hi"}]""";
        AddSet("day1", (assignment, 0));

        // Serialize with the app's global snake_case policy (Program.cs) to
        // prove the explicit JsonPropertyName attributes win.
        var appOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var json = JsonSerializer.Serialize(await _service.GetAssignmentsAsync("day1"), appOptions);

        Assert.DoesNotContain("slug", json);
        Assert.DoesNotContain("secret answer", json);   // SampleSolutionJson
        Assert.DoesNotContain("nonEmptyStdout", json);  // GradingJson
        Assert.DoesNotContain("hint", json);            // null hint omitted entirely
        Assert.Contains("\"content\":", json);
        Assert.Contains("\"lesson\":", json);
        Assert.Contains("\"kind\":\"code\"", json);
    }

    [Fact]
    public async Task ListAssignmentSets_ReturnsSummaries()
    {
        AddSet("day2");
        AddSet("day1");

        var sets = await _service.ListAssignmentSetsAsync();

        Assert.Equal(new[] { "day1", "day2" }, sets.Select(s => s.AssignmentSetId).ToArray());
        Assert.Equal("Title of day1", sets[0].DisplayTitle);
        Assert.True(await _service.ExistsAsync("day1"));
        Assert.False(await _service.ExistsAsync("day9"));
    }
}
