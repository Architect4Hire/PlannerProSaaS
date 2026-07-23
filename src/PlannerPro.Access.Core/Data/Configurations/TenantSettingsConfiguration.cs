using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data.Configurations;

public sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("TenantSettings");
        builder.HasKey(s => s.TenantId);
        builder.Property(s => s.TimeZone).HasMaxLength(100).IsRequired();

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantSettings>(s => s.TenantId)
            .HasPrincipalKey<Tenant>(t => t.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
