using cobblersBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cobblersBackend.Data.Configurations;

public class AssignmentSetAssignmentConfiguration : IEntityTypeConfiguration<AssignmentSetAssignment>
{
    public void Configure(EntityTypeBuilder<AssignmentSetAssignment> builder)
    {
        builder.HasKey(tst => tst.Id);
        builder.Property(tst => tst.Id)
               .ValueGeneratedOnAdd();

        // An assignment appears at most once per assignment set.
        builder.HasIndex(tst => new {tst.AssignmentSetId, tst.AssignmentId})
               .IsUnique();

        // No two assignments share a position within the same assignment set.
        builder.HasIndex(tst => new {tst.AssignmentSetId, tst.OrderIndex})
               .IsUnique();

        //Foreign Key
        builder.HasOne(tst => tst.AssignmentSet)
               .WithMany(ts => ts.Assignments)
               .HasForeignKey(tst => tst.AssignmentSetId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tst => tst.Assignment)
               .WithMany(t => t.AssignmentSets)
               .HasForeignKey(tst => tst.AssignmentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
