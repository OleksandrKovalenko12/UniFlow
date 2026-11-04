using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Infrastructure.Numbering;

namespace UniFlow.Infrastructure.Persistence.Configurations;

public class ApplicationNumberCounterConfiguration : IEntityTypeConfiguration<ApplicationNumberCounter>
{
    public void Configure(EntityTypeBuilder<ApplicationNumberCounter> builder)
    {
        builder.HasKey(c => c.Year);
        builder.Property(c => c.Year).ValueGeneratedNever();
    }
}