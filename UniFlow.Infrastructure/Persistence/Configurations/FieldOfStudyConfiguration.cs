using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class FieldOfStudyConfiguration : IEntityTypeConfiguration<FieldOfStudy> 
{
    public void Configure(EntityTypeBuilder<FieldOfStudy> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(200);

        builder.HasOne(f => f.Faculty)
            .WithMany()
            .HasForeignKey(f => f.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.FacultyId, f.Name, f.Level }).IsUnique();
    }
}
