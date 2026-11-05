using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.StudyLevel).HasMaxLength(32).IsRequired();
        b.Property(x => x.CriteriaJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ItemsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.AlgorithmVersion).HasMaxLength(32);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
