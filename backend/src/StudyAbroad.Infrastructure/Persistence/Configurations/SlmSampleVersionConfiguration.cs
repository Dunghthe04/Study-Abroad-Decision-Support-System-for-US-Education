using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class SlmSampleVersionConfiguration : IEntityTypeConfiguration<SlmSampleVersion>
{
    public void Configure(EntityTypeBuilder<SlmSampleVersion> b)
    {
        b.ToTable("slm_sample_versions", t => t.HasComment("Lịch sử phiên bản bất biến của mẫu huấn luyện SLM (UC-90)."));

        b.HasKey(x => x.Id);
        b.Ignore(x => x.UpdatedAt); // lịch sử chỉ thêm, không bao giờ sửa
        b.HasOne<SlmTrainingSample>().WithMany().HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.Version).IsRequired();
        b.Property(x => x.Action).HasMaxLength(16).IsRequired();
        b.Property(x => x.SampleType).HasMaxLength(32).IsRequired();
        b.Property(x => x.Question).IsRequired();
        b.Property(x => x.Profile).HasColumnType("jsonb");
        b.Property(x => x.PendingQuestion);
        b.Property(x => x.Intents).HasColumnType("jsonb").IsRequired().HasDefaultValue("[]");
        b.Property(x => x.Context).HasColumnType("jsonb");
        b.Property(x => x.Split).HasMaxLength(8).IsRequired();
        b.Property(x => x.SourceReview).HasMaxLength(16);
        b.Property(x => x.Source).HasMaxLength(256).IsRequired();

        b.HasIndex(x => new { x.SampleId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.SampleId, x.CreatedAt });
    }
}
