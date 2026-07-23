using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Tenancy;

/// <summary>
/// Asserts the mechanism, not just one hand-picked entity: every <see cref="ITenantScoped"/> type in
/// the model has the automatically-applied "Tenant" query filter, and the two entities that
/// deliberately opt out (<see cref="OutboxMessage"/>/<see cref="InboxMessage"/>) do not.
/// </summary>
public sealed class TenantScopedModelReflectionTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task EveryTenantScopedEntity_HasTheTenantQueryFilter()
    {
        await using var context = _factory.CreateContext();
        var model = context.Model;

        var tenantScopedTypes = model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType))
            .ToList();

        Assert.NotEmpty(tenantScopedTypes);

        foreach (var entityType in tenantScopedTypes)
        {
            // EF Core only stores a HasQueryFilter annotation on the root of a TPH hierarchy (which
            // is why SharedDbContext.OnModelCreating only ever applies it there too) — a derived type
            // inherits the filter but has no "declared" filter of its own, so walk to the root before
            // asserting.
            var root = entityType;
            while (root.BaseType is not null) root = root.BaseType;

            var filters = root.GetDeclaredQueryFilters();
            Assert.True(
                filters.Any(f => f.Key as string == "Tenant"),
                $"{entityType.ClrType.Name} implements ITenantScoped but its root {root.ClrType.Name} has no \"Tenant\" query filter.");
        }
    }

    [Fact]
    public async Task OutboxAndInboxMessages_DeliberatelyHaveNoTenantFilter()
    {
        await using var context = _factory.CreateContext();
        var model = context.Model;

        var outbox = model.FindEntityType(typeof(OutboxMessage))!;
        var inbox = model.FindEntityType(typeof(InboxMessage))!;

        Assert.False(typeof(ITenantScoped).IsAssignableFrom(outbox.ClrType));
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(inbox.ClrType));
        Assert.Empty(outbox.GetDeclaredQueryFilters());
        Assert.Empty(inbox.GetDeclaredQueryFilters());
    }

    public void Dispose() => _factory.Dispose();
}
