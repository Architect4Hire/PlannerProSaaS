using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data.Configurations;

/// <summary>
/// Hand-rolled equivalent of what <c>IdentityUserContext&lt;TUser,TKey&gt;.OnModelCreating</c> would
/// configure — <see cref="Data.AccessDbContext"/> can't inherit that base (it must inherit
/// <c>SharedDbContext</c> for tenant filtering/Outbox/Inbox instead), so the four Identity-shaped
/// tables are mapped by hand. Table named <c>Users</c>, not the legacy <c>AspNetUsers</c>, to match
/// this codebase's plain naming.
/// </summary>
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("UserNameIndex").IsUnique();
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex");

        builder.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
        builder.Property(u => u.UserName).HasMaxLength(256);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(256);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256);

        builder.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(c => c.UserId).IsRequired();
        builder.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(l => l.UserId).IsRequired();
        builder.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(t => t.UserId).IsRequired();
    }
}
