using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class AiCallConfiguration : IEntityTypeConfiguration<AiCall>
{
    public void Configure(EntityTypeBuilder<AiCall> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        b.Property(x => x.Model).HasMaxLength(64);
        b.Property(x => x.Error).HasMaxLength(1000);
        b.HasIndex(x => x.CreatedAt);
    }
}
