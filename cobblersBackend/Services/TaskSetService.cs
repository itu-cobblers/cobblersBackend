using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Services;

public class TaskSetService : ITaskSetService
{
    private readonly CobblersDbContext _db;

    public TaskSetService(CobblersDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TaskSetSummaryDto>> ListTaskSetsAsync() =>
        await _db.TaskSet
            .OrderBy(s => s.TaskSetId)
            .Select(s => new TaskSetSummaryDto(s.TaskSetId, s.DisplayTitle))
            .ToListAsync();

    public Task<bool> ExistsAsync(string tasksetId) =>
        _db.TaskSet.AnyAsync(s => s.TaskSetId == tasksetId);

    public async Task<IReadOnlyList<TaskDto>?> GetTasksAsync(string tasksetId)
    {
        if (!await ExistsAsync(tasksetId))
            return null;

        // Project only the student-safe columns — SampleSolutionJson,
        // GradingJson, and Slug never leave the database here (SCHEMA.md).
        var rows = await _db.TaskSetTask
            .Where(m => m.TaskSetId == tasksetId)
            .OrderBy(m => m.OrderIndex)
            .Select(m => new
            {
                m.Task.Id,
                m.Task.Kind,
                m.Task.Title,
                m.Task.Description,
                m.Task.Hint,
                m.Task.ContentJson,
            })
            .ToListAsync();

        return rows
            .Select(r => new TaskDto(
                r.Id,
                r.Kind.ToString().ToLowerInvariant(),
                r.Title,
                r.Description,
                r.Hint,
                JsonSerializer.Deserialize<JsonElement>(r.ContentJson)))
            .ToList();
    }
}
