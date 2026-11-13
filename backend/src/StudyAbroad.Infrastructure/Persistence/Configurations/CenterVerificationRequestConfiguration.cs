using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterVerificationRequestConfiguration : IEntityTypeConfiguration<CenterVerificationRequest>
{
    public void Configure(EntityTypeBuilder<CenterVerificationRequest> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LegalName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TaxCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(512).IsRequired();
        builder.Property(x => x.LegalRepresentative).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Website).HasMaxLength(512);

        builder.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("pending_review");
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.Property(x => x.ReviewNotes).HasMaxLength(2000);

        builder.HasIndex(x => x.CenterId);
        builder.HasIndex(x => x.TaxCode);
        builder.HasIndex(x => x.Status);

        builder.HasOne<StudyCenter>()
            .WithMany()
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

