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
    private readonly Func<string> _generateCode;

    public SessionService(CobblersDbContext db, Func<string>? generateCode = null)  
    {
        _db = db;
        _generateCode = generateCode ?? GenerateCode;
    }

    public async Task<string> CreateSessionAsync(string assignmentSetId)
    {

        var assignmentSetExists = await _db.AssignmentSet
            .AsNoTracking()
            .AnyAsync(t => t.AssignmentSetId == assignmentSetId);
        if (!assignmentSetExists)
            throw new InvalidOperationException($"Assignment set '{assignmentSetId}' not found");

        var session = new Session
        {
            SessionId = Guid.NewGuid().ToString(),
            Code = _generateCode(),
            AssignmentSetId = assignmentSetId
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
                session.Code = _generateCode(); // same tracked entity, retry
            }
        }
    }

    public async Task<GetSessionResponse?> GetSessionAsync(string code)
    {
        code = SessionCode.Normalize(code);
        // Ended rooms resolve as "not found" — a student can't join, and a
        // teacher can't restart the timer on, a room that's already closed.
        return await _db.Session
            .AsNoTracking()
            .Where(s => s.Code == code && s.Status == SessionStatus.Active)
            .Select(s => new GetSessionResponse(s.Code, s.AssignmentSetId))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> EndSessionAsync(string code)
    {
        code = SessionCode.Normalize(code);
        var session = await _db.Session.FirstOrDefaultAsync(s => s.Code == code);
        if (session is null) return false;

        session.Status = SessionStatus.Ended;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<GetSessionResponse?> GetTodayLatestActiveSessionAsync()
    {
        // "Today" in UTC — good enough for a single-timezone bootcamp; see SCHEMA.md.
        var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        return await _db.Session
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active && s.CreateAt >= todayStart)
            .OrderByDescending(s => s.CreateAt)
            .Select(s => new GetSessionResponse(s.Code, s.AssignmentSetId))
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