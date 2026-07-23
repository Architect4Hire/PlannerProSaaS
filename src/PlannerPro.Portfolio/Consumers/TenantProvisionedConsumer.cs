using PlannerPro.Contracts;
using PlannerPro.Portfolio.Core.Facade;
using PlannerPro.Shared.Messaging;

namespace PlannerPro.Portfolio.Consumers;

/// <summary>
/// Creates the new tenant's default "Internal" client. Contains no tenancy code at all — by the time
/// this is constructed, <c>ServiceBusProcessorHost</c> has already resolved
/// <see cref="Shared.Tenancy.ITenantContext"/> from the event envelope in this consumer's DI scope
/// (ADR-0009); every query and write below runs exactly like request-path code.
/// </summary>
public sealed class TenantProvisionedConsumer(IClientFacade facade) : IIntegrationEventConsumer<TenantProvisioned>
{
    public Task HandleAsync(TenantProvisioned @event, CancellationToken ct = default) =>
        facade.ProvisionInternalClientAsync(@event, ct);
}
