using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public const string SubmissionKeyIndex = "IX_Applications_ApplicantId_SubmissionKey";
    public const string InProgressIndex = "IX_Applications_EnrollmentId_ApplicationTypeId_InProgress";

    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Number).HasMaxLength(20);
        builder.HasIndex(a => a.Number).IsUnique();

        builder.Property(a => a.FormData).HasColumnType("jsonb");
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => a.Status);

        // The same submission sent twice cannot create two applications
        builder.HasIndex(a => new { a.ApplicantId, a.SubmissionKey })
            .IsUnique()
            .HasDatabaseName(SubmissionKeyIndex);

        // One application of a type in progress on given studies - also when two tabs submit
        // at the same moment. A partial index: decided and withdrawn applications do not count.
        var inProgress = string.Join(", ", Application.InProgressStatuses.Select(s => (int)s));
        builder.HasIndex(a => new { a.EnrollmentId, a.ApplicationTypeId })
            .IsUnique()
            .HasFilter($"\"Status\" IN ({inProgress})")
            .HasDatabaseName(InProgressIndex);

        // The index above covers only applications in progress, so the foreign key keeps its own
        // index for all of them - otherwise EF would drop it as "covered"
        builder.HasIndex(a => a.EnrollmentId);

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