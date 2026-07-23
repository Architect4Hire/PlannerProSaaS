namespace PlannerPro.Access.Core.Managers.Models.ServiceModels;

/// <summary>What a successful signup produces — enough for the controller to build the session
/// cookie and for the SPA to redirect to <c>/t/{slug}/board</c>.</summary>
public sealed record TenantProvisionedServiceModel(Guid TenantId, string Slug, Guid OwnerUserId, string OwnerEmail);
