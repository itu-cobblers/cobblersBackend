using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

/// <summary>
/// Backs `GET /api/assignments/{assignmentId}/solution` (CONTRACT.md, Solution).
/// Deliberately kind-agnostic and gate-free — see the interface's implementation
/// for why access control lives entirely on the frontend for this endpoint.
/// </summary>
public interface IAssignmentSolutionService
{
    /// <summary>Null when the assignment id doesn't exist at all (404); otherwise always answers.</summary>
    Task<SolutionResponseDto?> GetSolutionAsync(int assignmentId);
}
