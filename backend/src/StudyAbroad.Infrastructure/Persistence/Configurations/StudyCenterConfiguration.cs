using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class StudyCenterConfiguration : IEntityTypeConfiguration<StudyCenter>
{
    public void Configure(EntityTypeBuilder<StudyCenter> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Website).HasMaxLength(512);
        builder.Property(x => x.Address).HasMaxLength(512);
        builder.Property(x => x.City).HasMaxLength(128);
        // List<string> maps to PostgreSQL text[] with Npgsql.
        builder.Property(x => x.StudyLevels).HasColumnType("text[]");
        builder.Property(x => x.Services).HasColumnType("text[]");
    }
}
