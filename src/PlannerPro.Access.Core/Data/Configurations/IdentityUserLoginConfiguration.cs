using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlannerPro.Access.Core.Data.Configurations;

/// <summary>Framework type, no domain meaning — part of the hand-rolled Identity mapping, see
/// <see cref="ApplicationUserConfiguration"/>.</summary>
public sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("UserLogins");
        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey });
    }
}
