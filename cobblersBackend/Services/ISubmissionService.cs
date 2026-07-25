using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public interface ISubmissionService
{
    Task<SubmissionResponseDto?> SubmitAsync(int assignmentId, SubmissionRequestDto request);
}