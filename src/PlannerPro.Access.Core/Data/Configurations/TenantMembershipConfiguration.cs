using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("TenantMemberships");
        builder.HasKey(m => m.Id);

        // The only correct uniqueness for "is this user already a member of this tenant" — see the
        // XML doc on TenantMembership.
        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // No FK to ApplicationUser: Users is global/unfiltered while TenantMemberships is
        // tenant-scoped — a cross-table FK between a filtered and an unfiltered entity is fine
        // relationally, but deliberately not modeled as an EF navigation here to keep the two
        // scoping stories (global vs. tenant-scoped) from blurring into one aggregate.
    }
}
