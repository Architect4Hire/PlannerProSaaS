using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using PlannerPro.Shared.Exceptions;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Persistence;

/// <summary>
/// Layer 4 of tenant isolation (see <c>.claude/rules/tenancy.md</c>): stamps <c>TenantId</c> on newly
/// added <see cref="ITenantScoped"/> entities from the current <see cref="ITenantContext"/>, and throws
/// <see cref="CrossTenantWriteException"/> when a modified or deleted entity's <c>TenantId</c> doesn't
/// match. This catches what a query filter can't see — tracked entities, attached graphs, anything
/// that got into the change set some other way. Must be registered per service via
/// <c>AddInterceptors(...)</c> on the DbContext's own options — see
/// <see cref="SharedServiceCollectionExtensions.AddSharedTenancy"/>.
/// </summary>
public sealed class TenantSaveChangesInterceptor(
    ITenantContext tenant,
    ILogger<TenantSaveChangesInterceptor>? logger = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyTenantRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTenantRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyTenantRules(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    ApplyToAdded(entry.Entity);
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    ApplyToModifiedOrDeleted(entry.Entity);
                    break;
            }
        }
    }

    private void ApplyToAdded(ITenantScoped entity)
    {
        if (tenant.BypassFilters)
        {
            if (entity.TenantId == default)
            {
                // A system/seed write that forgot to set an explicit TenantId would silently persist
                // an orphan row no real tenant's filter can ever see again. Fail fast instead.
                throw new InvalidOperationException(
                    $"{entity.GetType().Name} was added under a bypass tenant context without an explicit TenantId.");
            }

            // Explicitly set by seed/system code — respect it rather than overwriting with Guid.Empty.
            return;
        }

        if (!tenant.IsResolved)
        {
            throw new InvalidOperationException(
                $"{entity.GetType().Name} was added while the tenant context was unresolved.");
        }

        entity.TenantId = tenant.TenantId;
    }

    private void ApplyToModifiedOrDeleted(ITenantScoped entity)
    {
        if (tenant.BypassFilters) return;

        if (!tenant.IsResolved)
        {
            // Without this, an unresolved context (TenantId defaults to Guid.Empty) modifying/deleting
            // a row that itself somehow has TenantId == Guid.Empty — e.g. one that reached the table
            // by a path outside this interceptor, such as raw SQL or a bulk import — would pass the
            // mismatch check below trivially (Guid.Empty == Guid.Empty) and silently succeed.
            throw new InvalidOperationException(
                $"{entity.GetType().Name} was modified or deleted while the tenant context was unresolved.");
        }

        if (entity.TenantId != tenant.TenantId)
        {
            logger?.LogWarning(
                "Blocked a cross-tenant write on {EntityType}: current tenant {CurrentTenantId}, entity tenant {AttemptedTenantId}.",
                entity.GetType().Name, tenant.TenantId, entity.TenantId);

            throw new CrossTenantWriteException(entity.GetType(), entity.TenantId, tenant.TenantId);
        }
    }
}
