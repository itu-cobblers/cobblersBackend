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
               .ValueGeneratedOnAdd()
               .HasDefaultValueSql("gen_random_uuid()");
        
        builder.Property(s => s.ContentJson)
               .HasColumnType("jsonb");

        builder.Property(s => s.ResultJson)
               .HasColumnType("jsonb");
        
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
               .OnDelete(DeleteBehavior.Restrict);
    }
}