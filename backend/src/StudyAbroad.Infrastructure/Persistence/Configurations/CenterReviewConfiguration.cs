using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterReviewConfiguration : IEntityTypeConfiguration<CenterReview>
{
    public void Configure(EntityTypeBuilder<CenterReview> b)
    {
        b.ToTable("center_reviews", t => t.HasCheckConstraint(
            "ck_center_reviews_rating",
            "rating >= 1 AND rating <= 5"));
        b.HasKey(x => x.Id);
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("visible");
        b.HasIndex(x => x.StudyCenterId);
    }
}
