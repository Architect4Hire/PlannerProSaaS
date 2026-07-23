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
/// slice. If it fails, no tenant row is ever attempted.
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
            var failures = createResult.Errors
                .Select(error => new ValidationFailure(nameof(viewModel.OwnerEmail), error.Description));
            throw new ValidationException(failures);
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

        await dataLayer.ProvisionAsync(tenant, settings, branding, ownerMembership, provisionedEvent, ct);

        return new TenantProvisionedServiceModel(tenantId, viewModel.Slug, user.Id, viewModel.OwnerEmail);
    }
}
