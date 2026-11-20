using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class FamilyGroupConfiguration : IEntityTypeConfiguration<FamilyGroup>
{
    public void Configure(EntityTypeBuilder<FamilyGroup> b)
    {
        b.ToTable("family_groups");
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.EndedReason).HasMaxLength(32);
        // Mỗi chủ tối đa 1 nhóm đang hoạt động
        b.HasIndex(x => x.OwnerUserId, "ux_family_groups_one_active_per_owner").IsUnique().HasFilter("status = 'active'");
    }
}
