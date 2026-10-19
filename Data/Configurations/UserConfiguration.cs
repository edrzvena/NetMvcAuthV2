using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetMvcAuthV2.Common.Enums;
using NetMvcAuthV2.Models;

namespace NetMvcAuthV2.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_Users_Email");

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasDefaultValue(UserRole.User);

        builder.Property(u => u.ResetPasswordTokenHash).HasMaxLength(64);
        builder.HasIndex(u => u.ResetPasswordTokenHash).HasDatabaseName("IX_Users_ResetPasswordTokenHash");

        builder.Property(u => u.SecurityStamp).IsRequired().HasMaxLength(64);
        builder.Property(u => u.FailedLoginCount).HasDefaultValue(0);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
