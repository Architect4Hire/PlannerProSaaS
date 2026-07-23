using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;

namespace PlannerPro.Access.Core.Facade;

public interface ITenantFacade
{
    Task<TenantProvisionedServiceModel> ProvisionAsync(SignupViewModel viewModel, Guid correlationId, CancellationToken ct = default);
}
