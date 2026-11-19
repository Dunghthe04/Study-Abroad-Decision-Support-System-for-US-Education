using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class KnowledgeBaseEntryHistoryConfiguration : IEntityTypeConfiguration<KnowledgeBaseEntryHistory>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseEntryHistory> b)
    {
        b.ToTable("knowledge_base_entry_history", t => t.HasComment("Lịch sử sửa và duyệt của mục kho tri thức."));

        b.HasKey(x => x.Id);
        b.HasOne<KnowledgeBaseEntry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.Action).HasMaxLength(16).IsRequired();
        b.Property(x => x.FromStatus).HasMaxLength(16);
        b.Property(x => x.ToStatus).HasMaxLength(16);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ChangedFields).HasMaxLength(256);

        b.HasIndex(x => new { x.EntryId, x.CreatedAt });
    }
}