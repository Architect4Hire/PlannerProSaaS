namespace PlannerPro.Access.Core.Managers.Models.ServiceModels;

/// <summary>What a successful login produces — enough for the controller to build the cookie
/// principal. Deliberately carries no tenant role: role is resolved per-request by the Gateway when a
/// <c>/api/t/{slug}/…</c> call is made, never baked into the session.</summary>
public sealed record SessionServiceModel(Guid UserId, string Email, bool IsPlatformAdmin);
