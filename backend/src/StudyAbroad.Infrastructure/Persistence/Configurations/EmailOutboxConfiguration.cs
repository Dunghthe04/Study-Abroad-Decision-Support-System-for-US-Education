using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> b)
    {
        b.ToTable("email_outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.DedupeKey).HasMaxLength(64);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(128);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasIndex(x => x.DedupeKey);
    }
}
