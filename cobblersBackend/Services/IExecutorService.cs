using cobblersBackend.DTOs;

namespace cobblersBackend.Services;

public interface IExecutorService
{
    public Task<ExecuteResponseDto> ExecuteAsync(string javaSource);
    public Task<ExecuteResponseDto> ExecuteAsync(IReadOnlyList<FileDto> files, string entryClass);
}