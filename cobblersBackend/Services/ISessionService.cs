using cobblersBackend.Data.Entities;
using cobblersBackend.Models;
namespace cobblersBackend.Services;

public interface ISessionService
{
    Task<string> CreateSessionAsync(string taskSetid); //-> code
    Task<GetSessionResponse?> GetSessionAsync(string code); //-> { code, tasksetid } or null
}