using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class UniversityRankingConfiguration : IEntityTypeConfiguration<UniversityRanking>
{
    public void Configure(EntityTypeBuilder<UniversityRanking> b)
    {
        b.HasKey(x => new { x.UniversityId, x.ListId });
        b.HasOne<University>().WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<RankingList>().WithMany().HasForeignKey(x => x.ListId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ListId).HasMaxLength(48);
        b.Property(x => x.IsTie).HasDefaultValue(false);
        b.Property(x => x.RankDisplay).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.ListId);
    }
}
