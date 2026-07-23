using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Contracts;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Data;

/// <summary>
/// Writes a brand-new tenant's Tenant/TenantSettings/TenantBranding/owner TenantMembership rows and
/// enqueues <see cref="TenantProvisioned"/> in one transaction. Same bypass reasoning as
/// <see cref="TenantRepository"/>/<see cref="TenantMembershipRepository"/> — no resolved
/// <see cref="ITenantContext"/> exists yet on this anonymous path, so this constructs its own
/// <see cref="SystemTenantContext"/>-backed <see cref="AccessDbContext"/> from the DI-registered
/// <see cref="DbContextOptions{TContext}"/> rather than accepting the ambient (unresolved) one.
/// </summary>
/// <remarks>
/// Enqueues the outbox row via a locally constructed <see cref="Outbox{TContext}"/> bound to this same
/// bypass <see cref="AccessDbContext"/> instance — deliberately not the DI-registered ambient
/// <see cref="IOutbox"/>, which is bound to a *different* context instance (the ambient, unresolved
/// one) and would break the single-transaction/single-<c>SaveChangesAsync</c> guarantee this method
/// exists to provide.
/// </remarks>
public sealed class TenantProvisioningRepository(DbContextOptions<AccessDbContext> options)
    : RepositoryBase<AccessDbContext>(new AccessDbContext(options, new SystemTenantContext())), ITenantProvisioningRepository, IAsyncDisposable
{
    // Unlike TenantRepository/TenantMembershipRepository, which build a per-call context inside a
    // local `using`, this context is a base-class field constructed once in the primary constructor —
    // DI has no way to see it and dispose it on its own, so this type must dispose it itself.
    public ValueTask DisposeAsync() => Context.DisposeAsync();

    public Task ProvisionAsync(
        Tenant tenant,
        TenantSettings settings,
        TenantBranding branding,
        TenantMembership ownerMembership,
        TenantProvisioned provisionedEvent,
        CancellationToken ct = default) =>
        ExecuteInTransactionAsync(async () =>
        {
            Context.Tenants.Add(tenant);
            Context.TenantSettings.Add(settings);
            Context.TenantBrandings.Add(branding);
            Context.TenantMemberships.Add(ownerMembership);

            await new Outbox<AccessDbContext>(Context).EnqueueAsync(provisionedEvent, ct);
        });
}
