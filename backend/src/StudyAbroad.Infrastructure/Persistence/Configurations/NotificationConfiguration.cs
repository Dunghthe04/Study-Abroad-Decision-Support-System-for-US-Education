using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Type).HasMaxLength(32).IsRequired();
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Body).HasMaxLength(2000);
        b.Property(x => x.LinkUrl).HasMaxLength(512);
        b.HasIndex(x => new { x.UserId, x.ReadAt });
    }
}
