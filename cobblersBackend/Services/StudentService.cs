using cobblersBackend.Data;
using cobblersBackend.Data.Entities;

namespace cobblersBackend.Services;

public class StudentService : IStudentService
{
    private readonly CobblersDbContext _db;

    public StudentService(CobblersDbContext db) =>  _db = db;

    public async Task UpsertStudentAsync(string studentId, string displayName)
    {
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
        await _db.SaveChangesAsync();
    }
}