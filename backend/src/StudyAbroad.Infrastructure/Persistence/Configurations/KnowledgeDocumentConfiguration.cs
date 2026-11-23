using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> b)
    {
        b.ToTable("knowledge_documents", t =>
        {
            var docTypes = string.Join(",", KnowledgeDocTypes.All.Select(x => $"'{x}'"));
            t.HasCheckConstraint("ck_knowledge_documents_doc_type", $"doc_type IN ({docTypes})");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.EntryNo).IsRequired();
        b.Property(x => x.DocType).HasMaxLength(32).IsRequired();
        b.Property(x => x.Label).HasMaxLength(64).IsRequired();
        b.Property(x => x.Question).IsRequired();
        b.Property(x => x.Answer).IsRequired();
        b.Property(x => x.Source);
        b.Property(x => x.SourceUrl);
        b.Property(x => x.RetrievedAt).IsRequired();
        b.Property(x => x.TopicCode).HasMaxLength(64);
        b.Property(x => x.ImageUrl);
        b.Property(x => x.Note);
        b.Property(x => x.EmbeddingHash).HasMaxLength(64);
        b.Property(x => x.IngestedAt);

        b.HasIndex(x => x.EntryNo).IsUnique();
        b.HasIndex(x => x.DocType);
        b.HasIndex(x => x.Label);
        b.HasIndex(x => x.TopicCode);
    }
}
