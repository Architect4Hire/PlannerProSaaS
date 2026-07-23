using FluentValidation;
using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;

namespace PlannerPro.Access.Core.Facade;

public sealed class TenantFacade(IValidator<SignupViewModel> validator, ITenantProvisioningBusiness business) : ITenantFacade
{
    public async Task<TenantProvisionedServiceModel> ProvisionAsync(
        SignupViewModel viewModel, Guid correlationId, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(viewModel, ct);
        return await business.ProvisionAsync(viewModel, correlationId, ct);
    }
}
