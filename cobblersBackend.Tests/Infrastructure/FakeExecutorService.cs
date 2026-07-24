using cobblersBackend.DTOs;
using cobblersBackend.Services;

namespace cobblersBackend.Tests.Infrastructure;

public sealed class FakeExecutorService : IExecutorService
{
    private readonly ExecuteResponseDto _response;
    public FakeExecutorService(ExecuteResponseDto response) => _response = response;
    public Task<ExecuteResponseDto> ExecuteAsync(string javaSource) => Task.FromResult(_response);
}