using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.Ignore(e => e.YearOfStudy);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FieldOfStudy)
            .WithMany()
            .HasForeignKey(e => e.FieldOfStudyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.StudentId, e.FieldOfStudyId }).IsUnique();
    }
}