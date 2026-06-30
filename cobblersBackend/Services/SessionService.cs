
using cobblersBackend.Models;
using System.Collections.Concurrent;

namespace cobblersBackend.Services;

public class SessionService
{
    private readonly ConcurrentDictionary<string, Session> _sessionStore = new();
    private static readonly string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Session CreateSession()
    {
        while(true)
        {
            var code = GenerateCode();
            var session = new Session { Code = code, CreatedAt = DateTime.UtcNow };

            if (_sessionStore.TryAdd(code, session))
            return session;
        }
    }

    private static string GenerateCode()
    {
        return new string([.. Enumerable.Range(0,4).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)])]);
    }


}