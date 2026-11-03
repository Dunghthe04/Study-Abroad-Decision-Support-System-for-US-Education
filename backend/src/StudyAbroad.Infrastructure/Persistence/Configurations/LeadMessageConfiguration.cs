using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class LeadMessageConfiguration : IEntityTypeConfiguration<LeadMessage>
{
    public void Configure(EntityTypeBuilder<LeadMessage> b)
    {
        b.ToTable("lead_messages");
        b.HasKey(x => x.Id);
        b.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RemovedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Content).HasMaxLength(4000).IsRequired();
        b.Property(x => x.FlagReason).HasMaxLength(500);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("visible");
        b.HasIndex(x => x.LeadId);
    }
}
