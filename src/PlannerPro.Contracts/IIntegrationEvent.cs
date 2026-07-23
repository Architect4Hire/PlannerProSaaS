namespace PlannerPro.Contracts;

public interface IIntegrationEvent
{
    Guid Id { get; }
    Guid TenantId { get; }
    Guid CorrelationId { get; }
    Guid? CausationId { get; }
    Guid ActorId { get; }
}
