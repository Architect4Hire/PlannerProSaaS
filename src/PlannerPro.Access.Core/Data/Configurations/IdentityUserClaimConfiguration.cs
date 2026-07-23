using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlannerPro.Access.Core.Data.Configurations;

/// <summary>Framework type, no domain meaning — part of the hand-rolled Identity mapping, see
/// <see cref="ApplicationUserConfiguration"/>.</summary>
public sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("UserClaims");
        builder.HasKey(c => c.Id);
    }
}
