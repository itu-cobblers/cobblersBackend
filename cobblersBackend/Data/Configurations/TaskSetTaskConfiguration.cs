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
        
        // A task appears at most once per taskset.
        builder.HasIndex(tst => new {tst.TaskSetId, tst.TaskId})
               .IsUnique();

        // No two tasks share a position within the same taskset.
        builder.HasIndex(tst => new {tst.TaskSetId, tst.OrderIndex})
               .IsUnique();
        
        //Foreign Key
        builder.HasOne(tst => tst.TaskSet)
               .WithMany(ts => ts.Tasks)
               .HasForeignKey(tst => tst.TaskSetId)
               .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(tst => tst.Task)
               .WithMany(t => t.TaskSets)
               .HasForeignKey(tst => tst.TaskId)
               // Pinned: same reason as SubmissionConfiguration — CLR rename to
               // Assignment must not rename the existing DB constraint.
               .HasConstraintName("fk_task_set_task_task_task_id")
               .OnDelete(DeleteBehavior.Cascade);
    }
}