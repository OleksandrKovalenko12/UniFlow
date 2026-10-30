using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.Property(f => f.Code).HasMaxLength(8);
        builder.Property(f => f.Name).HasMaxLength(200);

        builder.HasIndex(f => f.Code).IsUnique();
    }
}