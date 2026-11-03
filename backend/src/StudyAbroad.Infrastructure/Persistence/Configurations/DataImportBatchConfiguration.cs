using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class DataImportBatchConfiguration : IEntityTypeConfiguration<DataImportBatch>
{
    public void Configure(EntityTypeBuilder<DataImportBatch> b)
    {
        b.ToTable("data_import_batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.BatchCode).HasMaxLength(64).IsRequired();
        b.Property(x => x.SourceName).HasMaxLength(128);
        b.Property(x => x.DataYear).HasMaxLength(32);
        b.Property(x => x.CheckedBy).HasMaxLength(128).HasDefaultValue("USAS Research Team");
        b.Property(x => x.Status).HasMaxLength(32).HasDefaultValue("verified");
        b.HasIndex(x => x.BatchCode).IsUnique();
    }
}
