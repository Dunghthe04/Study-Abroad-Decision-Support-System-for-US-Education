using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        b.Property(x => x.Type).HasMaxLength(32).IsRequired();
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.Property(x => x.DisplayOrder).HasDefaultValue(0);
        b.Property(x => x.License).HasMaxLength(100);
        b.Property(x => x.Author).HasMaxLength(300);
        b.Property(x => x.SourcePageUrl).HasMaxLength(1000);

        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Type });
        b.HasIndex(x => new { x.EntityId, x.DisplayOrder });
    }
}
