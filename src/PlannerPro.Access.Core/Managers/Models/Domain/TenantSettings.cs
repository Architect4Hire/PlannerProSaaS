using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// Per-tenant configuration for sprint cadence and overload detection — the HLD is explicit that
/// these are settings, not constants: "the overload threshold and sprint cadence are per-tenant
/// settings, not constants." One-to-one with <see cref="Tenant"/>; <see cref="TenantId"/> is both the
/// primary key and the foreign key.
/// </summary>
public sealed class TenantSettings : ITenantScoped
{
    public Guid TenantId { get; set; }

    public int SprintLengthDays { get; set; } = 14;

    public int OverloadThresholdPoints { get; set; } = 40;

    /// <summary>IANA time zone id, e.g. <c>"UTC"</c>, <c>"America/Chicago"</c>.</summary>
    public string TimeZone { get; set; } = "UTC";
}
