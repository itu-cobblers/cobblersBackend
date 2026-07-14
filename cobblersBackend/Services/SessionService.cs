using System.Security.Cryptography;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace cobblersBackend.Services;

public class SessionService : ISessionService
{
    // Charset for room codes: uppercase, no ambiguous 0/O or 1/I (CONTRACT.md).
    // Moved here from SessionStore — the DB is now the authority on codes.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 4;
    private const int MaxCodeRetries = 5;

    private readonly CobblersDbContext _db;

    public SessionService(CobblersDbContext db) => _db = db;

    public async Task<string> CreateSessionAsync(string taskSetId)
    {
        
        var tasksetExists = await _db.TaskSet
            .AsNoTracking()
            .AnyAsync(t => t.TaskSetId == taskSetId);
        if (!tasksetExists)
            throw new InvalidOperationException($"Taskset '{taskSetId}' not found");

        var session = new Session
        {
            SessionId = Guid.NewGuid().ToString(),
            Code = GenerateCode(),
            TaskSetId = taskSetId
            // CreateAt: DB-owned (DEFAULT now()), never set here
        };
        _db.Session.Add(session);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync();
                return session.Code;
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException { SqlState: "23505" }
                      && attempt < MaxCodeRetries)
            {
                session.Code = GenerateCode(); // same tracked entity, retry
            }
        }
    }

    public async Task<SessionDto?> GetSessionAsync(string code)
    {
        code = SessionCode.Normalize(code);
        return await _db.Session
            .AsNoTracking()
            .Where(s => s.Code == code)
            .Select(s => new SessionDto(s.Code, s.TaskSetId))
            .FirstOrDefaultAsync();
    }

    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

}