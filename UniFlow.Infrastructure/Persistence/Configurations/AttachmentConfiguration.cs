using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");

        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.OriginalFileName).HasMaxLength(255);
        builder.Property(a => a.OriginalContentType).HasMaxLength(100);
        builder.Property(a => a.StoragePath).HasMaxLength(500);
        builder.Property(a => a.PdfStoragePath).HasMaxLength(500);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}