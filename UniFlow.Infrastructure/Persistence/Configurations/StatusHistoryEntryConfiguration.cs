using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class StatusHistoryEntryConfiguration : IEntityTypeConfiguration<StatusHistoryEntry>
{
    public void Configure(EntityTypeBuilder<StatusHistoryEntry> builder)
    {
        builder.ToTable("StatusHistoryEntries");

        builder.Property(h => h.Comment).HasMaxLength(2000);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(h => h.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}