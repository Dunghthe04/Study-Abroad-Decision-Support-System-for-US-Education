using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class RankingListConfiguration : IEntityTypeConfiguration<RankingList>
{
    public void Configure(EntityTypeBuilder<RankingList> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(48);
        b.Property(x => x.Publisher).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.ListGroup).HasMaxLength(32);
        b.Property(x => x.RankType).HasMaxLength(32);
        b.Property(x => x.Edition).HasMaxLength(64);
        b.Property(x => x.Url).HasMaxLength(512);
    }
}
