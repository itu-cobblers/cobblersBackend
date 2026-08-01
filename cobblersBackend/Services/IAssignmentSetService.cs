using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

/// <summary>Read-side queries behind CONTRACT.md's Assignments endpoints.</summary>
public interface IAssignmentSetService
{
    Task<IReadOnlyList<AssignmentSetSummaryDto>> ListAssignmentSetsAsync();
    Task<IReadOnlyList<AssignmentDto>?> GetAssignmentsAsync(string assignmentSetId, bool includeSolution = false);
    Task<bool> ExistsAsync(string assignmentSetId);
}