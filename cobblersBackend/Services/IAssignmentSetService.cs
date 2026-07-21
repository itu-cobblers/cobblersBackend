using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

/// <summary>Read-side queries behind CONTRACT.md's Assignments endpoints.</summary>
public interface IAssignmentSetService
{
    Task<IReadOnlyList<AssignmentSetSummaryDto>> ListAssignmentSetsAsync();

    Task<bool> ExistsAsync(string assignmentSetId);

    /// <summary>
    /// The set's assignments sorted by OrderIndex (the array index IS the
    /// assignment's position — see CONTRACT.md). Null when the set doesn't
    /// exist, distinguishing "unknown set" (404) from "empty set" (200 []).
    /// </summary>
    Task<IReadOnlyList<AssignmentDto>?> GetAssignmentsAsync(string assignmentSetId);
}
