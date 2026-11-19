using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class SlmSampleHistoryConfiguration : IEntityTypeConfiguration<SlmSampleHistory>
{
    public void Configure(EntityTypeBuilder<SlmSampleHistory> b)
    {
        b.ToTable("slm_sample_history", t => t.HasComment("Lịch sử sửa và duyệt của mẫu SLM."));

        b.HasKey(x => x.Id);
        b.Ignore(x => x.UpdatedAt); // lịch sử chỉ thêm, không bao giờ sửa
        b.HasOne<SlmTrainingSample>().WithMany().HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.Action).HasMaxLength(16).IsRequired();
        b.Property(x => x.FromStatus).HasMaxLength(16);
        b.Property(x => x.ToStatus).HasMaxLength(16);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ChangedFields).HasMaxLength(256);

        b.HasIndex(x => new { x.SampleId, x.CreatedAt });
    }
}
