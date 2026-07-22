namespace cobblersBackend.Services;

public interface IStudentService
{
    Task UpsertStudentAsync(string studentId, string displayName);
}