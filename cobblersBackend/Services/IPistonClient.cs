using cobblersBackend.Models;

namespace cobblersBackend.Services;
public interface IPistonClient
{
    Task<PistonExecuteResponse> ExecuteAsync(string language, IReadOnlyList<PistonFile> files);
}