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
                     v => Enum.Parse<TaskKind>(v, ignoreCase: true))
               .HasColumnType("text");

        // Pin the table name: the CLR type/DbSet renamed to Assignment, but the
        // schema keeps `task` (no migration needed — the rename is C#-only).
        builder.ToTable("task", t => t.HasCheckConstraint(
              "ck_task_kind",
              "kind IN ('code', 'predict', 'project')"));
    }
}