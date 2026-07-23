using PlannerPro.Access.Core.Managers.Models.ServiceModels;

namespace PlannerPro.Access.Core.Business;

public interface IAuthBusiness
{
    /// <summary>Returns <c>null</c> for both an unknown email and a wrong password — deliberately
    /// indistinguishable, so login isn't a user-enumeration oracle.</summary>
    Task<SessionServiceModel?> AuthenticateAsync(string email, string password, CancellationToken ct = default);
}
