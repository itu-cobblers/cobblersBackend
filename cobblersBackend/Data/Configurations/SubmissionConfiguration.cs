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
               .HasColumnName("jsonb");

        builder.Property(s => s.ResultJson)
               .HasColumnName("jsonb");
        
        builder.Property(s => s.SubmittedAt)
               .HasColumnName("timestamptz");
        
        //Foreign Keys
        builder.HasOne(s => s.Session)
               .WithMany()
               .HasForeignKey(s => s.SessionId)
               .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(s => s.Student)
               .WithMany()
               .HasForeignKey(s => s.StudentId)
               .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(s => s.Task)
               .WithMany()
               .HasForeignKey(s => s.TaskId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}