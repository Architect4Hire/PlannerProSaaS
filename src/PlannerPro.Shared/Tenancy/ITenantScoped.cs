namespace PlannerPro.Shared.Tenancy;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
