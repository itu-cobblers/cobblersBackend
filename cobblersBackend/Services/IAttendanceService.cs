using cobblersBackend.Models;
namespace cobblersBackend.Services;

public interface IAttendanceService
{
    Task RecordAttendanceAsync(string code, string studentId, string displayName);
    Task<IReadOnlyList<StudentDto>> GetAttendanceAsync(string code);

}