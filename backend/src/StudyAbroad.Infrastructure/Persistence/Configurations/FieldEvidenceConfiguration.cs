using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class FieldEvidenceConfiguration : IEntityTypeConfiguration<FieldEvidence>
{
    public void Configure(EntityTypeBuilder<FieldEvidence> b)
    {
        b.ToTable("field_evidence");
        b.HasKey(x => x.Id);
        b.HasOne<University>().WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.FieldName).HasMaxLength(64).IsRequired();
        b.Property(x => x.SourceUrl).HasMaxLength(512);
        b.Property(x => x.SourceSection).HasMaxLength(64);
        b.Property(x => x.VerifiedBy).HasMaxLength(128).HasDefaultValue("USAS Audit Panel");
        b.Property(x => x.Status).HasMaxLength(32).HasDefaultValue("verified");
        b.HasIndex(x => x.UniversityId);
    }
}
