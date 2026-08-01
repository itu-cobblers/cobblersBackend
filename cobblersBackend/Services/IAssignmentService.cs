using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public interface IAssignmentService
{
    Task<IReadOnlyList<AssignmentDto>> GetAssignmentsByIdsAsync(int[] ids, bool includeSolution = false);
}
