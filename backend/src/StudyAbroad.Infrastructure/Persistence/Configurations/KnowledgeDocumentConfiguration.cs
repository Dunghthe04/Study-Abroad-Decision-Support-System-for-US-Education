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
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Question).IsRequired();
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.SourceUrl).HasMaxLength(1000);
        b.Property(x => x.SourceType).HasMaxLength(64).IsRequired().HasDefaultValue("official");
        b.Property(x => x.SourceNote).HasMaxLength(256);
        b.Property(x => x.Topic).HasMaxLength(128);
        b.Property(x => x.StudyLevel).HasMaxLength(32).IsRequired().HasDefaultValue("general");
        b.Property(x => x.Scope).HasMaxLength(64).IsRequired().HasDefaultValue("system_advisory");
        b.Property(x => x.Status).HasMaxLength(16).IsRequired().HasDefaultValue("draft");
        b.Property(x => x.EmbeddingHash).HasMaxLength(64);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.Status, x.Scope, x.StudyLevel });
    }

}
