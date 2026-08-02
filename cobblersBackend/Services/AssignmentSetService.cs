using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Services;

public class AssignmentSetService : IAssignmentSetService
{
    private readonly CobblersDbContext _db;
    private readonly IAssignmentService _assignmentService; 

    public AssignmentSetService(CobblersDbContext db, IAssignmentService assignmentService)
    {
        _db = db;
        _assignmentService = assignmentService;
    }

    public async Task<IReadOnlyList<AssignmentSetSummaryDto>> ListAssignmentSetsAsync() =>
        await _db.AssignmentSet
            .OrderBy(s => s.AssignmentSetId)
            .Select(s => new AssignmentSetSummaryDto(s.AssignmentSetId, s.DisplayTitle))
            .ToListAsync();

    public Task<bool> ExistsAsync(string assignmentSetId) =>
        _db.AssignmentSet.AnyAsync(s => s.AssignmentSetId == assignmentSetId);
    
    public async Task<IReadOnlyList<AssignmentDto>?> GetAssignmentsAsync(string assignmentSetId, bool includeSolution = false)
    {
        if (!await ExistsAsync(assignmentSetId))
            return null;

        var ids = await _db.AssignmentSetAssignment
            .Where(m => m.AssignmentSetId == assignmentSetId)
            .OrderBy(m => m.OrderIndex)
            .Select(m => m.AssignmentId)
            .ToArrayAsync();

        return await _assignmentService.GetAssignmentsByIdsAsync(ids, includeSolution);
    }
}
