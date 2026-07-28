using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Services;

/// <summary>
/// `SampleSolutionJson` presence is the only thing this service checks — no
/// `Kind` branch, no `Submission` lookup, no timer/session awareness. Every
/// "when is a student actually allowed to see this" rule (per-kind, per-mode —
/// see CONTRACT.md's Solution section and SCHEMA.md's reveal-rule decision) is
/// the frontend's job: it decides when to call this endpoint and when to show
/// the "Show answer" button. This keeps the backend simple at the cost of a
/// student being able to fetch a solution early via devtools — an accepted
/// tradeoff (see SCHEMA.md).
/// </summary>
public class AssignmentSolutionService : IAssignmentSolutionService
{
    private readonly CobblersDbContext _db;

    public AssignmentSolutionService(CobblersDbContext db)
    {
        _db = db;
    }

    public async Task<SolutionResponseDto?> GetSolutionAsync(int assignmentId)
    {
        var sampleSolutionJson = await _db.Assignment
            .Where(a => a.Id == assignmentId)
            .Select(a => new { Exists = true, a.SampleSolutionJson })
            .FirstOrDefaultAsync();

        if (sampleSolutionJson is null) return null; // no such assignment — 404

        if (sampleSolutionJson.SampleSolutionJson is null)
            return new SolutionResponseDto(false, null);

        return new SolutionResponseDto(true, JsonSerializer.Deserialize<JsonElement>(sampleSolutionJson.SampleSolutionJson));
    }
}
