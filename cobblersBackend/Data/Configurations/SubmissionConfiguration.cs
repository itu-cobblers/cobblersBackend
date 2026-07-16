using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.HasKey(s => s.SubId);
        builder.Property(s => s.SubId)
               .ValueGeneratedNever();
        
        builder.Property(s => s.ContentJson)
               .HasColumnType("jsonb");

        builder.Property(s => s.ResultJson)
               .HasColumnType("jsonb");

        builder.Property(s => s.SubmittedAt)
               .ValueGeneratedOnAdd()
               .HasDefaultValueSql("now()");

        //Foreign Keys
        builder.HasOne(s => s.Session)
               .WithMany()
               .HasForeignKey(s => s.SessionId)
               .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(s => s.Student)
               .WithMany()
               .HasForeignKey(s => s.StudentId)
               .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(s => s.Task)
               .WithMany(t => t.Submissions)
               .HasForeignKey(s => s.TaskId)
               // Pinned: EF derives FK names from the principal CLR type, which is
               // now Assignment — keep the original name so the rename stays C#-only.
               .HasConstraintName("fk_submission_task_task_id")
               .OnDelete(DeleteBehavior.Restrict);
    }
}