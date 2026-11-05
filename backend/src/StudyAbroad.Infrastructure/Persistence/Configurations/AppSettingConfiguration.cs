using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.ValueJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Key).IsUnique();
    }
}
