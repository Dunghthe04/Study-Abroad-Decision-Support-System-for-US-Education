using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class PolicyAlertConfiguration : IEntityTypeConfiguration<PolicyAlert>
{
    public void Configure(EntityTypeBuilder<PolicyAlert> b)
    {
        b.ToTable("policy_alerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.AlertType).HasMaxLength(32).IsRequired().HasDefaultValue("visa");
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.SourceUrl).HasMaxLength(512);
        b.Property(x => x.Severity).HasMaxLength(32).HasDefaultValue("info");
    }
}
