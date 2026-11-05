using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(64);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.DetailJson).HasColumnType("jsonb");
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.ActorUserId);
    }
}
