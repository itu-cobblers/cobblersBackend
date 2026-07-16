using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Data;

public class CobblersDbContext: DbContext
{
    public CobblersDbContext(DbContextOptions<CobblersDbContext> options) : base(options)
    {
        
    }

    public DbSet<Attendance> Attendance { get; set;}
    public DbSet<Session> Session { get; set;}
    public DbSet<Student> Student { get; set;}
    public DbSet<Submission> Submission { get; set;}
    public DbSet<Assignment> Assignment { get; set;}
    public DbSet<TaskSet> TaskSet { get; set;}
    public DbSet<TaskSetTask> TaskSetTask { get; set;}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //Apply All Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CobblersDbContext).Assembly);
    }

}