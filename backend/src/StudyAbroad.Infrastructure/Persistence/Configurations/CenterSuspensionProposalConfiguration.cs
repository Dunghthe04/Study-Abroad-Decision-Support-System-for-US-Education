using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterSuspensionProposalConfiguration : IEntityTypeConfiguration<CenterSuspensionProposal>
{
    public void Configure(EntityTypeBuilder<CenterSuspensionProposal> b)
    {
        b.ToTable("center_suspension_proposals");
        b.HasKey(x => x.Id);
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("pending");
        b.Property(x => x.DecisionNote).HasMaxLength(1000);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        // Mỗi trung tâm tối đa một đề nghị đang chờ
        b.HasIndex(x => x.StudyCenterId).IsUnique().HasFilter("status = 'pending'");
    }
}
