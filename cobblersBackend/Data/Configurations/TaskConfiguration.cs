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
               .HasColumnName("jsonb");
        
        builder.Property(t => t.SampleSolutionJson)
               .HasColumnName("jsonb");
        
        builder.Property(t => t.Kind)
               .HasConversion<string>()
               .HasColumnType("text");
    }
}