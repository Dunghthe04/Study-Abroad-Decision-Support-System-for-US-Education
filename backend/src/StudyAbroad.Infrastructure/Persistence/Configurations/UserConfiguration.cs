using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Configurations
{
    //Được tự nạp nhờ ApplyConfigurationsFromAssembly trong AppDbContext,  không cần đăng ký.
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
            builder.HasIndex(x => x.Email).IsUnique();
            builder.Property(x => x.PasswordHash).HasMaxLength(60).IsRequired();
            builder.Property(x => x.FullName).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Role).HasMaxLength(16).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
            // [USAS-362] SĐT liên hệ tối đa 32 ký tự, không bắt buộc (nullable)
            builder.Property(x => x.Phone).HasMaxLength(32);
        }
    }
}
