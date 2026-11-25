using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class DeletedBuyerRetentionConfiguration : IEntityTypeConfiguration<DeletedBuyerRetention>
{
    public void Configure(EntityTypeBuilder<DeletedBuyerRetention> b)
    {
        b.ToTable("deleted_buyer_retentions");
        b.HasKey(x => x.Id);
        b.Property(x => x.BuyerCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(256).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.BuyerCode).IsUnique();
        b.HasIndex(x => x.RetainUntilUtc);
    }
}
