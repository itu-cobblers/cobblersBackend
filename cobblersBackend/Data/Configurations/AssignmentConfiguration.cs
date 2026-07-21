using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
               .ValueGeneratedOnAdd();

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.ContentJson)
               .HasColumnType("jsonb");

        builder.Property(t => t.SampleSolutionJson)
               .HasColumnType("jsonb");

        builder.Property(t => t.GradingJson)
               .HasColumnType("jsonb");

        builder.Property(t => t.Kind)
               .HasConversion(
                     v => v.ToString().ToLowerInvariant(),
                     v => Enum.Parse<AssignmentKind>(v, ignoreCase: true))
               .HasColumnType("text");

        builder.ToTable("assignment", t => t.HasCheckConstraint(
              "ck_assignment_kind",
              "kind IN ('code', 'predict', 'project')"));
    }
}
