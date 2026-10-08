using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class TranscriptScoreConfiguration : IEntityTypeConfiguration<TranscriptScore>
{
    public void Configure(EntityTypeBuilder<TranscriptScore> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.TermName).HasMaxLength(64).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(128).IsRequired();
        b.Property(x => x.Score).HasPrecision(6, 2);
        b.Property(x => x.RawScore).HasMaxLength(16);
        b.Property(x => x.Credits).HasPrecision(4, 1);
        b.HasIndex(x => new { x.StudentProfileId, x.TermOrder });
    }
}
