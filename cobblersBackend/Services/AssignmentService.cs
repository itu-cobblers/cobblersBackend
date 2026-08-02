using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Services;


public class AssignmentService : IAssignmentService
{
    private readonly CobblersDbContext _db;

    public AssignmentService(CobblersDbContext db)
    {
        _db = db;
    }
    
    public async Task<IReadOnlyList<AssignmentDto>> GetAssignmentsByIdsAsync(int[] ids, bool includeSolution = false)
    {
        if (ids is null || ids.Length == 0)
            return new List<AssignmentDto>();

        var assignments = await _db.Assignment.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();

        var assignmentDict = assignments.ToDictionary(a => a.Id);
        
        var result = new List<AssignmentDto>();

        foreach (var id in ids)
        {
            if (assignmentDict.TryGetValue(id, out var a))
            {
                JsonElement? solution = null;
                if (includeSolution && a.SampleSolutionJson != null) 
                {
                    solution = JsonSerializer.Deserialize<JsonElement>(a.SampleSolutionJson);
                }

                result.Add(new AssignmentDto(
                    a.Id,
                    a.Kind.ToString().ToLowerInvariant(), 
                    a.Title,
                    a.Description,
                    a.LessonJson is null ? null : JsonSerializer.Deserialize<JsonElement>(a.LessonJson),
                    a.Hint,
                    JsonSerializer.Deserialize<JsonElement>(a.ContentJson),
                    solution
                ));
            }
        }

        return result;
    }
}