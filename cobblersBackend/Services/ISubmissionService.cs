using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public interface ISubmissionService
{
    Task<SubmissionResponseDto?> SubmitAsync(int assignmentId, SubmissionRequestDto request);
    Task<IReadOnlyList<SubmissionHistoryDto>> GetHistoryAsync(string studentId);
    Task<SubmissionDetailDto?> GetSubmissionAsync(Guid subId);

    /// <summary>Returns <c>SampleSolutionJson</c> for the assignment, or <c>null</c> when the id does not exist.</summary>
    Task<SolutionResponseDto?> GetSolutionAsync(int assignmentId);
}