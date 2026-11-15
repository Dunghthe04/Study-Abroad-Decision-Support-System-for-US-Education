using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterVerificationDocumentConfiguration : IEntityTypeConfiguration<CenterVerificationDocument>
{
    public void Configure(EntityTypeBuilder<CenterVerificationDocument> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FileUrl).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();

        builder.HasIndex(x => x.RequestId);
        builder.HasIndex(x => x.CenterId);

        builder.HasOne<StudyCenter>()
            .WithMany()
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

