using cobblersBackend.Data.Entities;
using cobblersBackend.Models;
namespace cobblersBackend.Services;

public interface ISessionService
{
    Task<string> CreateSessionAsync(string assignmentSetId); //-> code
    Task<GetSessionResponse?> GetSessionAsync(string code); //-> { code, assignmentSetId } or null
    Task<bool> EndSessionAsync(string code); //-> false if no such session
    Task<GetSessionResponse?> GetTodayLatestActiveSessionAsync(); //-> today's newest still-active room, or null
}
