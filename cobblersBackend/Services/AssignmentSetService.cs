using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Services;

public class AssignmentSetService : IAssignmentSetService
{
    private readonly CobblersDbContext _db;

    public AssignmentSetService(CobblersDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AssignmentSetSummaryDto>> ListAssignmentSetsAsync() =>
        await _db.AssignmentSet
            .OrderBy(s => s.AssignmentSetId)
            .Select(s => new AssignmentSetSummaryDto(s.AssignmentSetId, s.DisplayTitle))
            .ToListAsync();

    public Task<bool> ExistsAsync(string assignmentSetId) =>
        _db.AssignmentSet.AnyAsync(s => s.AssignmentSetId == assignmentSetId);

    public async Task<IReadOnlyList<AssignmentDto>?> GetAssignmentsAsync(string assignmentSetId)
    {
        if (!await ExistsAsync(assignmentSetId))
            return null;

        // Project only the student-safe columns — SampleSolutionJson,
        // GradingJson, and Slug never leave the database here (SCHEMA.md).
        var rows = await _db.AssignmentSetAssignment
            .Where(m => m.AssignmentSetId == assignmentSetId)
            .OrderBy(m => m.OrderIndex)
            .Select(m => new
            {
                m.Assignment.Id,
                m.Assignment.Kind,
                m.Assignment.Title,
                m.Assignment.Description,
                m.Assignment.LessonJson,
                m.Assignment.Hint,
                m.Assignment.ContentJson,
            })
            .ToListAsync();

        return rows
            .Select(r => new AssignmentDto(
                r.Id,
                r.Kind.ToString().ToLowerInvariant(),
                r.Title,
                r.Description,
                r.LessonJson is null ? null : JsonSerializer.Deserialize<JsonElement>(r.LessonJson),
                r.Hint,
                JsonSerializer.Deserialize<JsonElement>(r.ContentJson)))
            .ToList();
    }
}
