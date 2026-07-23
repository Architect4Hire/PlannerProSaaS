using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Portfolio.Core.Managers.Models.Domain;

/// <summary>A customer Portfolio serves work for, on behalf of one tenant. Every tenant gets one
/// "Internal" client automatically at signup (see <c>TenantProvisionedConsumer</c>) — a home for work
/// that isn't billed to an external client.</summary>
public sealed class Client : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
