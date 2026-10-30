using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Number).HasMaxLength(20);
        builder.HasIndex(a => a.Number).IsUnique();

        builder.Property(a => a.FormData).HasColumnType("jsonb");
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => a.Status);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Enrollment)
            .WithMany()
            .HasForeignKey(a => a.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ApplicationType)
            .WithMany()
            .HasForeignKey(a => a.ApplicationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(a => a.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.Attachments)
            .WithOne()
            .HasForeignKey(at => at.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(a => a.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}