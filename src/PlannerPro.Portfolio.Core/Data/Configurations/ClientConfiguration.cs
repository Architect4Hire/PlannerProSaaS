using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlannerPro.Portfolio.Core.Managers.Models.Domain;

namespace PlannerPro.Portfolio.Core.Data.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);

        // No FK to Tenant — that table lives in accessdb, a different database ("no shared database,
        // ever"). Just an index for query performance; the query filter is what actually scopes reads.
        builder.HasIndex(c => c.TenantId);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
    }
}
