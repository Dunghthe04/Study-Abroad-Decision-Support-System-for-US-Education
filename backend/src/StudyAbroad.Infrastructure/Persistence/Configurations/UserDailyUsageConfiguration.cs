using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class UserDailyUsageConfiguration : IEntityTypeConfiguration<UserDailyUsage>
{
    public void Configure(EntityTypeBuilder<UserDailyUsage> b)
    {
        b.ToTable("user_daily_usages");
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.UsageDate }).IsUnique();
    }
}
