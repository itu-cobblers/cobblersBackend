using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class TaskSetConfiguration : IEntityTypeConfiguration<TaskSet>
{
    public void Configure(EntityTypeBuilder<TaskSet> builder)
    {
        builder.HasKey(ts => ts.TaskSetId);
        builder.Property(ts => ts.TaskSetId)
               .ValueGeneratedNever();
    }
}