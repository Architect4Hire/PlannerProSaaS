using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        // Deliberately global-unique, not (TenantId, Slug) — see the XML doc on Tenant.Slug.
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.Slug).HasMaxLength(63).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.StripeCustomerId).HasMaxLength(200);
        builder.Property(t => t.StripeSubscriptionId).HasMaxLength(200);
    }
}
