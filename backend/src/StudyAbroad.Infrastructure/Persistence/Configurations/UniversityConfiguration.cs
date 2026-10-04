using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.City).HasMaxLength(128);
        b.Property(x => x.State).HasMaxLength(2);
        b.Property(x => x.Website).HasMaxLength(512);
        b.Property(x => x.Control).HasMaxLength(16);
        b.Property(x => x.AcceptanceRate).HasPrecision(5, 4);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.State);
    }
}
