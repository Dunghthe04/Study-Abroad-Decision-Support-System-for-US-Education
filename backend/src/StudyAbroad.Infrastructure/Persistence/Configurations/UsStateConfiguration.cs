using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class UsStateConfiguration : IEntityTypeConfiguration<UsState>
{
    public void Configure(EntityTypeBuilder<UsState> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(2).IsRequired();
        b.Property(x => x.Name).HasMaxLength(64).IsRequired();
        b.Property(x => x.Region).HasMaxLength(16).IsRequired();
        b.Property(x => x.IsPopular).HasDefaultValue(false);
        b.HasIndex(x => x.Code).IsUnique();
    }
}
