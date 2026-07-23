using PlannerPro.Contracts;

namespace PlannerPro.Shared.Tests.TestSupport;

internal sealed record TestEvent(
    Guid Id,
    Guid TenantId,
    Guid CorrelationId,
    Guid? CausationId,
    Guid ActorId,
    string Payload) : IIntegrationEvent;
