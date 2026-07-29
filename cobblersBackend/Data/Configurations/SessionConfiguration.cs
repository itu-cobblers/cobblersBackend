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

        builder.HasIndex(s => s.Code)
               .IsUnique();

        builder.Property(s => s.CreateAt)
               .ValueGeneratedOnAdd()
               .HasDefaultValueSql("now()");

        builder.Property(s => s.Status)
               .HasConversion(
                     v => v.ToString().ToLowerInvariant(),
                     v => Enum.Parse<SessionStatus>(v, ignoreCase: true))
               .HasColumnType("text")
               .HasDefaultValue(SessionStatus.Active);

        builder.HasOne(s => s.AssignmentSet)
               .WithMany(ts => ts.Sessions)
               .HasForeignKey(s => s.AssignmentSetId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("session", s => s.HasCheckConstraint(
              "ck_session_status",
              "status IN ('active', 'ended')"));
    }
}
