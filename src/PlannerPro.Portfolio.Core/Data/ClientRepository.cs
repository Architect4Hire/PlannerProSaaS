using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Managers.Models.Domain;
using PlannerPro.Shared.Persistence;

namespace PlannerPro.Portfolio.Core.Data;

/// <summary>
/// Unlike Access's provisioning write, this needs no bypass context: <c>ServiceBusProcessorHost</c>
/// resolves <see cref="Shared.Tenancy.ITenantContext"/> from the event envelope in a fresh DI scope
/// BEFORE this (or anything else in that scope) is constructed, so the DI-injected
/// <see cref="PortfolioDbContext"/> and <see cref="IInbox"/> below are already tenant-resolved and
/// share the same underlying context instance — one <c>ExecuteInTransactionAsync</c> call is enough
/// for the inbox check, the insert, and the inbox mark to be one atomic unit.
/// </summary>
public sealed class ClientRepository(PortfolioDbContext context, IInbox inbox)
    : RepositoryBase<PortfolioDbContext>(context), IClientRepository
{
    public Task ProvisionInternalClientAsync(TenantProvisioned provisionedEvent, CancellationToken ct = default) =>
        ExecuteInTransactionAsync(async () =>
        {
            if (await inbox.IsHandledAsync(provisionedEvent.Id, ct))
            {
                return;
            }

            Context.Clients.Add(new Client
            {
                Id = Guid.NewGuid(),
                TenantId = provisionedEvent.TenantId,
                Name = "Internal",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });

            await inbox.MarkHandledAsync(provisionedEvent.Id, nameof(TenantProvisioned), ct);
        });
}
