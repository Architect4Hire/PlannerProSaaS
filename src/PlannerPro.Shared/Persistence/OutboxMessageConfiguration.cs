using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlannerPro.Shared.Persistence;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.EventTypeName).IsRequired();
        builder.Property(m => m.Content).IsRequired();
        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => m.ProcessedOnUtc);
    }
}
