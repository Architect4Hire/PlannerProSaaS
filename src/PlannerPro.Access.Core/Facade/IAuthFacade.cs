using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;

namespace PlannerPro.Access.Core.Facade;

public interface IAuthFacade
{
    Task<SessionServiceModel?> LoginAsync(LoginViewModel viewModel, CancellationToken ct = default);
}
