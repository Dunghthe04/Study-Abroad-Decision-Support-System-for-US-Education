using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class MajorConfiguration : IEntityTypeConfiguration<Major>
{
    public void Configure(EntityTypeBuilder<Major> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.CipCode).HasMaxLength(7);
        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => x.CipCode).IsUnique();
    }
}
