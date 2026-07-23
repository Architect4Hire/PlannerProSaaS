using Microsoft.AspNetCore.Identity;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;

namespace PlannerPro.Access.Core.Business;

/// <summary>
/// <see cref="UserManager{TUser}"/>/<see cref="SignInManager{TUser}"/> are the repository layer for
/// this global slice — see the XML doc on <see cref="ApplicationUser"/> — so there's no separate
/// Data/Repository pair here; that would just re-wrap what <c>UserOnlyStore</c> already provides.
/// </summary>
public sealed class AuthBusiness(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    : IAuthBusiness
{
    // A precomputed hash for a user that doesn't exist, verified below on the unknown-email path.
    // Without this, an unknown email returns immediately while a wrong password pays for a real
    // PBKDF2 verification first — a measurable timing difference that lets an attacker enumerate
    // registered emails even though both paths return the identical null/response. Computed once,
    // not per request; the password itself is discarded and never needs to match anything.
    private static readonly ApplicationUser DummyUser = new();
    private static readonly string DummyPasswordHash =
        new PasswordHasher<ApplicationUser>().HashPassword(DummyUser, Guid.NewGuid().ToString());

    public async Task<SessionServiceModel?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, password);
            return null;
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded) return null;

        return new SessionServiceModel(user.Id, user.Email!, user.IsPlatformAdmin);
    }
}
