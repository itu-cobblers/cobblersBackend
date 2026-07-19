using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class TaskConfiguration : IEntityTypeConfiguration<Entities.Task>
{
    public void Configure(EntityTypeBuilder<Entities.Task> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
               .ValueGeneratedOnAdd();

        builder.Property(t => t.ContentJson)
               .HasColumnType("jsonb");
        
        builder.Property(t => t.SampleSolutionJson)
               .HasColumnType("jsonb");
        
        builder.Property(t => t.Kind)
               .HasConversion(
                     v => v.ToString().ToLowerInvariant(),
                     v => Enum.Parse<TaskKind>(v, ignoreCase: true))
               .HasColumnType("text");

        builder.ToTable(t => t.HasCheckConstraint(
              "ck_task_kind",
              "kind IN ('code', 'predict', 'project')"));
    }
}