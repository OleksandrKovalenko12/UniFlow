using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class ApplicationTypeConfiguration : IEntityTypeConfiguration<ApplicationType>
{
    public void Configure(EntityTypeBuilder<ApplicationType> builder)
    {
        builder.Property(t => t.Code).HasMaxLength(50);
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(1000);

        builder.HasIndex(t => t.Code).IsUnique();
    }
}
