using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.HasKey(p => p.UserId);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<StudentProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.AlbumNumber).HasMaxLength(10);
        builder.Property(p => p.PrivateEmail).HasMaxLength(256);

        builder.HasIndex(p => p.AlbumNumber).IsUnique();
    }
}