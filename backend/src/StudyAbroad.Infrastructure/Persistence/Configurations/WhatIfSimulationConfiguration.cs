using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class WhatIfSimulationConfiguration : IEntityTypeConfiguration<WhatIfSimulation>
{
    public void Configure(EntityTypeBuilder<WhatIfSimulation> b)
    {
        b.ToTable("what_if_simulations");
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.SimulatedProfileJson).HasColumnType("jsonb");
        b.Property(x => x.SimulatedRecommendationsJson).HasColumnType("jsonb");
        b.HasIndex(x => x.UserId);
    }
}
