using FluentValidation;
using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;

namespace PlannerPro.Access.Core.Facade;

public sealed class AuthFacade(IValidator<LoginViewModel> validator, IAuthBusiness business) : IAuthFacade
{
    public async Task<SessionServiceModel?> LoginAsync(LoginViewModel viewModel, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(viewModel, ct);
        return await business.AuthenticateAsync(viewModel.Email, viewModel.Password, ct);
    }
}
