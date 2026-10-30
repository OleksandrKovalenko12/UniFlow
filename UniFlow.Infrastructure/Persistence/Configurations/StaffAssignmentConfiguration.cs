using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class StaffAssignmentConfiguration : IEntityTypeConfiguration<StaffAssignment>
{
    public void Configure(EntityTypeBuilder<StaffAssignment> builder)
    {
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Faculty)
            .WithMany()
            .HasForeignKey(s => s.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.UserId, s.FacultyId }).IsUnique();
    }
}