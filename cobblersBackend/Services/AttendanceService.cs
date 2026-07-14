using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Services;

public class AttendanceService : IAttendanceService
{
    private readonly CobblersDbContext _db;

    public AttendanceService(CobblersDbContext db) => _db = db;

    /// <summary>
    /// Record that a student joined a session: upserts the Student row, then
    /// inserts the Attendance row if it doesn't exist. Idempotent — rejoins
    /// (reconnects, refreshes) are the common case, not the exception.
    /// One SaveChanges = one transaction: student + attendance commit together.
    /// </summary>
    public async System.Threading.Tasks.Task RecordAttendanceAsync(string code, string studentId, string displayName)
    {
        code = SessionCode.Normalize(code);

        var session = await _db.Session
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code)
            ?? throw new InvalidOperationException($"No session with code '{code}'");

        // Upsert the student. FindAsync checks the change tracker before
        // querying, so it's the natural "get by PK" here.
        var student = await _db.Student.FindAsync(studentId);
        if (student is null)
        {
            _db.Student.Add(new Student {Id = studentId, DisplayName = displayName});
        } else if(student.DisplayName != displayName)
        {
            student.DisplayName = displayName; // latest name wins (tracked → UPDATE on save)
        }

        // Insert attendance only on first join. JoinedAt is DB-owned (DEFAULT now())
        // and never touched on rejoin — so it means "first joined", by design.
        var attended = await _db.Attendance.AnyAsync(
            a => a.StudentId == studentId && a.SessionId == session.SessionId);
        if(!attended)
        {
            _db.Attendance.Add(new Attendance
            {
                StudentId = studentId,
                SessionId = session.SessionId
            });
        }

        await _db.SaveChangesAsync();
    }
    /// <summary>
    /// Historical roster: everyone who has ever joined this session, in join
    /// order. This is *not* the live roster (SessionStore owns "connected now").
    /// </summary>
    public async Task<IReadOnlyList<StudentDto>> GetAttendanceAsync(string code)
    {
        code = SessionCode.Normalize(code);

        return await _db.Attendance
                .AsNoTracking()
                .Where(a => a.Session.Code == code)
                .OrderBy(a => a.JoinedAt)
                .Select(a => new StudentDto(a.Student.Id, a.Student.DisplayName))
                .ToListAsync();
    }
}