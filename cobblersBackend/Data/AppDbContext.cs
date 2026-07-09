using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Data;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        
    }

    public DbSet<Attendance> Attendance { get; set;}
    public DbSet<Session> Session { get; set;}
    public DbSet<Student> Student { get; set;}
    public DbSet<Submission> Submission { get; set;}
    public DbSet<Entities.Task> Task { get; set;}
    public DbSet<TaskSet> TaskSet { get; set;}
    public DbSet<TaskSetTask> TaskSetTask { get; set;}

}