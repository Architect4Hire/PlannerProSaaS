using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlannerPro.Access.Core.Data.Configurations;

/// <summary>Framework type, no domain meaning — part of the hand-rolled Identity mapping, see
/// <see cref="ApplicationUserConfiguration"/>.</summary>
public sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("UserTokens");
        builder.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
    }
}
