using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.VerifiedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Question);
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(128);
        b.Property(x => x.StudyLevel).HasMaxLength(32).IsRequired().HasDefaultValue("general");
        b.Property(x => x.Stage).HasMaxLength(64);
        b.Property(x => x.DocType).HasMaxLength(32).IsRequired().HasDefaultValue("faq");
        b.Property(x => x.Scope).HasMaxLength(64).IsRequired().HasDefaultValue("system_advisory");
        b.Property(x => x.Status).HasMaxLength(16).IsRequired().HasDefaultValue("draft");
        b.Property(x => x.Quote);
        b.Property(x => x.SourceType).HasMaxLength(64).IsRequired().HasDefaultValue("official");
        b.Property(x => x.SourceNote).HasMaxLength(256);
        b.Property(x => x.SourceUrl).HasMaxLength(1000);
        b.Property(x => x.RetrievedAt);
        b.Property(x => x.ReviewDueAt);
        b.Property(x => x.Version).HasDefaultValue(1);
        b.Property(x => x.Embedding).HasColumnType("vector(1024)");
        b.Property(x => x.IngestedAt);

        b.HasIndex(x => new { x.Status, x.StudyLevel, x.DocType });
        b.HasIndex(x => x.Scope);
        b.HasIndex(x => x.Topic);
    }
}
