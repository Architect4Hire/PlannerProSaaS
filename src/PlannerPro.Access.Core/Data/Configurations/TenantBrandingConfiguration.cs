using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data.Configurations;

public sealed class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("TenantBrandings");
        builder.HasKey(b => b.TenantId);
        builder.Property(b => b.ProductName).HasMaxLength(200);
        builder.Property(b => b.AccentColor).HasMaxLength(9);
        builder.Property(b => b.SurfaceColor).HasMaxLength(9);
        builder.Property(b => b.ThemeMode).HasMaxLength(10).IsRequired();

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantBranding>(b => b.TenantId)
            .HasPrincipalKey<Tenant>(t => t.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
