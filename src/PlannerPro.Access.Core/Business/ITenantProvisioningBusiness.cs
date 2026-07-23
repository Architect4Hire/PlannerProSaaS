using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;

namespace PlannerPro.Access.Core.Business;

public interface ITenantProvisioningBusiness
{
    Task<TenantProvisionedServiceModel> ProvisionAsync(SignupViewModel viewModel, Guid correlationId, CancellationToken ct = default);
}
