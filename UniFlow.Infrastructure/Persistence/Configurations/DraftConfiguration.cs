using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class DraftConfiguration : IEntityTypeConfiguration<Draft>
{
    public void Configure(EntityTypeBuilder<Draft> builder)
    {
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.FormData).HasColumnType("jsonb");
        builder.Ignore(d => d.ExpiresAt);

        // One unfinished new application of each type per student - corrections do not count,
        // a student may correct one application and start another of the same type
        builder.HasIndex(d => new { d.StudentId, d.ApplicationTypeId })
            .IsUnique()
            .HasFilter("\"ApplicationId\" IS NULL");

        // The index above leaves out corrections, so the student's list keeps its own index
        builder.HasIndex(d => d.StudentId);

        // One correction per application
        builder.HasIndex(d => d.ApplicationId).IsUnique();

        // The cleanup looks for old drafts
        builder.HasIndex(d => d.UpdatedAt);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Enrollment)
            .WithMany()
            .HasForeignKey(d => d.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ApplicationType)
            .WithMany()
            .HasForeignKey(d => d.ApplicationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Application)
            .WithMany()
            .HasForeignKey(d => d.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // The files go with the draft - after submission they belong to attachments instead
        builder.HasMany(d => d.Files)
            .WithOne()
            .HasForeignKey(f => f.DraftId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Files).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class DraftFileConfiguration : IEntityTypeConfiguration<DraftFile>
{
    public void Configure(EntityTypeBuilder<DraftFile> builder)
    {
        builder.ToTable("DraftFiles");

        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.OriginalFileName).HasMaxLength(255);
        builder.Property(f => f.ContentType).HasMaxLength(100);
        builder.Property(f => f.StoragePath).HasMaxLength(500);
        builder.Property(f => f.PdfStoragePath).HasMaxLength(500);
    }
}