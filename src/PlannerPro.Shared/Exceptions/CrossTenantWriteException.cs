using Microsoft.AspNetCore.Http;

namespace PlannerPro.Shared.Exceptions;

/// <summary>
/// Thrown by <see cref="Persistence.TenantSaveChangesInterceptor"/> when a tracked entity's
/// <c>TenantId</c> doesn't match the current tenant on save. The message is deliberately generic and
/// carries no distinguishing error code — <see cref="SharedExceptionHandler"/> puts <see
/// cref="Exception.Message"/> straight into the response body, and naming "cross-tenant" there would
/// itself confirm the row exists in another tenant (the same 404-never-403 doctrine applied here).
/// Diagnostic detail is on typed properties for server-side logging only.
/// </summary>
public sealed class CrossTenantWriteException(Type entityType, Guid attemptedTenantId, Guid currentTenantId)
    : DomainException("The requested resource was not found.", StatusCodes.Status404NotFound)
{
    public Type EntityType { get; } = entityType;

    public Guid AttemptedTenantId { get; } = attemptedTenantId;

    public Guid CurrentTenantId { get; } = currentTenantId;
}
