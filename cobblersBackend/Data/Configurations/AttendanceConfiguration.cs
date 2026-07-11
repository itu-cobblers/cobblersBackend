using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.HasKey(a => new { a.StudentId, a.SessionId});

        builder.Property(a => a.JoinedAt)
               .ValueGeneratedOnAdd()
               .HasDefaultValueSql("now()");

        //Foreign Keys
        builder.HasOne(a => a.Student)
               .WithMany()
               .HasForeignKey(a => a.StudentId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.HasOne(a => a.Session)
               .WithMany()
               .HasForeignKey(a => a.SessionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}