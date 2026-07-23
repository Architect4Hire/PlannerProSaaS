using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Core.Managers.Models.ServiceModels;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Contracts;

namespace PlannerPro.Access.Core.Business;

/// <summary>
/// Maps <see cref="SignupViewModel"/> → the new tenant's whole starting graph, applies the two
/// uniqueness rules (slug, email) that only a database round-trip can answer, and builds
/// <see cref="TenantProvisioned"/> with its full envelope. <see cref="ApplicationUser"/> creation goes
/// through <see cref="UserManager{TUser}"/> first and outside the tenant-write transaction — same
/// reasoning as <see cref="AuthBusiness"/>'s doc comment: Identity is the repository for this global
/// slice. If it fails, no tenant row is ever attempted. If the tenant write fails after the user was
/// already created, the user is deleted again (best-effort) rather than left orphaned.
/// </summary>
public sealed class TenantProvisioningBusiness(
    UserManager<ApplicationUser> userManager,
    ITenantRepository tenantRepository,
    ITenantProvisioningDataLayer dataLayer) : ITenantProvisioningBusiness
{
    public async Task<TenantProvisionedServiceModel> ProvisionAsync(
        SignupViewModel viewModel, Guid correlationId, CancellationToken ct = default)
    {
        var existingTenant = await tenantRepository.FindBySlugAsync(viewModel.Slug, ct);
        if (existingTenant is not null)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(viewModel.Slug), "This slug is already in use.")]);
        }

        var user = new ApplicationUser { UserName = viewModel.OwnerEmail, Email = viewModel.OwnerEmail };
        var createResult = await userManager.CreateAsync(user, viewModel.OwnerPassword);
        if (!createResult.Succeeded)
        {
            throw new ValidationException(MapIdentityErrors(createResult, viewModel));
        }

        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Slug = viewModel.Slug,
            Name = viewModel.TenantName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        var settings = new TenantSettings { TenantId = tenantId };
        var branding = new TenantBranding { TenantId = tenantId };
        var ownerMembership = new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Role = TenantRole.Owner,
        };

        var provisionedEvent = new TenantProvisioned(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: correlationId,
            CausationId: null,
            ActorId: user.Id,
            Slug: viewModel.Slug,
            TenantName: viewModel.TenantName);

        try
        {
            await dataLayer.ProvisionAsync(tenant, settings, branding, ownerMembership, provisionedEvent, ct);
        }
        catch (Exception)
        {
            // The Identity user above already committed, outside this transaction. If the tenant write
            // failed for any reason, don't leave a permanently orphaned account behind — with
            // RequireUniqueEmail on, that email could never sign up again otherwise. Best-effort: a
            // failure deleting it here must not hide the original error.
            await userManager.DeleteAsync(user);

            // A concurrent signup for the same slug can race past the check above and lose only at the
            // database's unique index. Recognize that case by re-checking rather than parsing a
            // provider-specific exception, so the caller gets the same clean 400 either way.
            if (await tenantRepository.FindBySlugAsync(viewModel.Slug, ct) is not null)
            {
                throw new ValidationException(
                    [new ValidationFailure(nameof(viewModel.Slug), "This slug is already in use.")]);
            }

            throw;
        }

        return new TenantProvisionedServiceModel(tenantId, viewModel.Slug, user.Id, viewModel.OwnerEmail);
    }

    private static IEnumerable<ValidationFailure> MapIdentityErrors(IdentityResult result, SignupViewModel viewModel) =>
        result.Errors.Select(error => new ValidationFailure(
            error.Code.StartsWith("Password", StringComparison.Ordinal)
                ? nameof(viewModel.OwnerPassword)
                : nameof(viewModel.OwnerEmail),
            error.Description));
}
