using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.DTOs;
using cobblersBackend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskEntity = cobblersBackend.Data.Entities.Task;

namespace cobblersBackend.Tests;

/// <summary>
/// Query-shape tests for TaskSetService against SQLite in-memory (the real
/// DB is Postgres; ordering/projection logic is what's under test here).
/// </summary>
public class TaskSetServiceTest : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CobblersDbContext _db;
    private readonly TaskSetService _service;

    public TaskSetServiceTest()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new CobblersDbContext(
            new DbContextOptionsBuilder<CobblersDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new TaskSetService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private TaskEntity AddTask(string slug, string contentJson = """{"starter": "public class Main {}"}""")
    {
        var task = new TaskEntity
        {
            Slug = slug,
            Kind = TaskKind.Code,
            Title = $"Title of {slug}",
            Description = "desc",
            Hint = null,
            ContentJson = contentJson,
            SampleSolutionJson = "\"the secret answer\"",
            GradingJson = """{"op": "nonEmptyStdout"}""",
        };
        _db.Task.Add(task);
        return task;
    }

    private void AddSet(string tasksetId, params (TaskEntity task, int order)[] members)
    {
        _db.TaskSet.Add(new TaskSet { TaskSetId = tasksetId, DisplayTitle = $"Title of {tasksetId}" });
        _db.SaveChanges(); // assign task ids before wiring memberships
        foreach (var (task, order) in members)
            _db.TaskSetTask.Add(new TaskSetTask { TaskSetId = tasksetId, TaskId = task.Id, OrderIndex = order });
        _db.SaveChanges();
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTasks_SortsByOrderIndex_NotByIdOrInsertOrder()
    {
        var third = AddTask("third");
        var first = AddTask("first");
        var second = AddTask("second");
        AddSet("day1", (third, 2), (first, 0), (second, 1));

        var tasks = await _service.GetTasksAsync("day1");

        Assert.NotNull(tasks);
        Assert.Equal(
            new[] { "Title of first", "Title of second", "Title of third" },
            tasks.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTasks_TaskInTwoSets_AppearsInBoth()
    {
        var shared = AddTask("shared");
        AddSet("day1", (shared, 0));
        AddSet("solo", (shared, 0));

        var day1 = await _service.GetTasksAsync("day1");
        var solo = await _service.GetTasksAsync("solo");

        Assert.Equal(shared.Id, Assert.Single(day1!).Id);
        Assert.Equal(shared.Id, Assert.Single(solo!).Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTasks_UnknownTaskset_ReturnsNull_ButEmptySetReturnsEmpty()
    {
        AddSet("empty-set");

        Assert.Null(await _service.GetTasksAsync("nope"));
        Assert.Empty((await _service.GetTasksAsync("empty-set"))!);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTasks_ContentIsParsedJson_AndKindIsLowercase()
    {
        var task = AddTask("with-content", """{"starter": "code here", "stdin": "50\n"}""");
        AddSet("day1", (task, 0));

        var dto = Assert.Single((await _service.GetTasksAsync("day1"))!);

        Assert.Equal("code", dto.Kind);
        Assert.Equal("code here", dto.Content.GetProperty("starter").GetString());
        Assert.Equal("50\n", dto.Content.GetProperty("stdin").GetString());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTasks_SerializedDto_LeaksNothing_AndUsesCamelCase()
    {
        var task = AddTask("sensitive");
        AddSet("day1", (task, 0));

        // Serialize with the app's global snake_case policy (Program.cs) to
        // prove the explicit JsonPropertyName attributes win.
        var appOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var json = JsonSerializer.Serialize(await _service.GetTasksAsync("day1"), appOptions);

        Assert.DoesNotContain("slug", json);
        Assert.DoesNotContain("secret answer", json);   // SampleSolutionJson
        Assert.DoesNotContain("nonEmptyStdout", json);  // GradingJson
        Assert.DoesNotContain("hint", json);            // null hint omitted entirely
        Assert.Contains("\"content\":", json);
        Assert.Contains("\"kind\":\"code\"", json);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListTaskSets_ReturnsSummaries()
    {
        AddSet("day2");
        AddSet("day1");

        var sets = await _service.ListTaskSetsAsync();

        Assert.Equal(new[] { "day1", "day2" }, sets.Select(s => s.TasksetId).ToArray());
        Assert.Equal("Title of day1", sets[0].DisplayTitle);
        Assert.True(await _service.ExistsAsync("day1"));
        Assert.False(await _service.ExistsAsync("day9"));
    }
}
