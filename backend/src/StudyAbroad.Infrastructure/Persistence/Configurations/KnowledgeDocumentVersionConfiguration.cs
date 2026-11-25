using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentVersionConfiguration : IEntityTypeConfiguration<KnowledgeDocumentVersion>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocumentVersion> b)
    {
        b.ToTable("knowledge_document_versions", t => t.HasComment("Lịch sử phiên bản bất biến của mục kho tri thức (UC-113)."));

        b.HasKey(x => x.Id);
        b.Ignore(x => x.UpdatedAt); // lịch sử chỉ thêm, không bao giờ sửa
        b.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.Version).IsRequired();
        b.Property(x => x.Action).HasMaxLength(16).IsRequired();
        b.Property(x => x.DocType).HasMaxLength(32).IsRequired();
        b.Property(x => x.Label).HasMaxLength(64).IsRequired();
        b.Property(x => x.Question).IsRequired();
        b.Property(x => x.Answer).IsRequired();
        b.Property(x => x.Source);
        b.Property(x => x.SourceUrl);
        b.Property(x => x.RetrievedAt).IsRequired();
        b.Property(x => x.TopicCode).HasMaxLength(64);
        b.Property(x => x.Note);

        b.HasIndex(x => new { x.DocumentId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.DocumentId, x.CreatedAt });
    }
}
