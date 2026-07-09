using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class TaskSetTaskConfiguration : IEntityTypeConfiguration<TaskSetTask>
{
    public void Configure(EntityTypeBuilder<TaskSetTask> builder)
    {
        builder.HasKey(tst => tst.Id);
        builder.Property(tst => tst.Id)
               .ValueGeneratedOnAdd();
        
        // Unique constraint on (TasksetId, TaskId)
        builder.HasIndex(tst => new {tst.TasksetId, tst.TaskId})
               .IsUnique();
        
        //Foreign Key
        builder.HasOne(tst => tst.TaskSet)
               .WithMany()
               .HasForeignKey(tst => tst.TasksetId)
               .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(tst => tst.Task)
               .WithMany()
               .HasForeignKey(tst => tst.TaskId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}