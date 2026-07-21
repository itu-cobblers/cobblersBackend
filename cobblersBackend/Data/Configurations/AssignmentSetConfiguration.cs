using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class AssignmentSetConfiguration : IEntityTypeConfiguration<AssignmentSet>
{
    public void Configure(EntityTypeBuilder<AssignmentSet> builder)
    {
        builder.HasKey(ts => ts.AssignmentSetId);
        builder.Property(ts => ts.AssignmentSetId)
               .ValueGeneratedNever();
    }
}
