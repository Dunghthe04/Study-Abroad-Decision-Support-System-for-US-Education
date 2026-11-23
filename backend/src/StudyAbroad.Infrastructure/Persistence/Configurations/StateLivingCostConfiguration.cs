using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class StateLivingCostConfiguration : IEntityTypeConfiguration<StateLivingCost>
{
    public void Configure(EntityTypeBuilder<StateLivingCost> b)
    {
        b.ToTable(t => t.HasComment("[USAS-2067] Chỉ số giá khu vực theo bang (BEA RPP, Mỹ = 100), mỗi bang mỗi năm một dòng. Không phải USD."));
        b.HasKey(x => x.Id);
        b.Property(x => x.StateCode).HasMaxLength(2).IsRequired();
        b.Property(x => x.IndexAllItems).HasPrecision(6, 2);
        b.Property(x => x.IndexHousingRents).HasPrecision(6, 2);
        b.Property(x => x.IndexGoods).HasPrecision(6, 2);
        b.Property(x => x.IndexOtherServices).HasPrecision(6, 2);
        b.Property(x => x.SourceUrl).HasMaxLength(512).IsRequired();
        b.HasOne<UsState>().WithMany().HasForeignKey(x => x.StateCode).HasPrincipalKey(s => s.Code).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.StateCode, x.Year }).IsUnique();
    }
}
