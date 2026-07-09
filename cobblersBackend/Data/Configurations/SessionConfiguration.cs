using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(s => s.SessionId);
        builder.Property(s => s.SessionId)
               .ValueGeneratedNever();
               
        builder.HasIndex(s => new { s.Code, s.Year})
               .IsUnique();

        builder.HasOne(s => s.TaskSet)
               .WithMany(ts => ts.Sessions)
               .HasForeignKey(s => s.TaskSetId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}