using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>
/// Per-tenant white-label appearance (ADR-0019): product name, logo, favicon, accent/surface colours,
/// login tagline, theme mode. One-to-one with <see cref="Tenant"/>; <see cref="TenantId"/> is both the
/// primary key and the foreign key. The anonymous public branding endpoint that serves this to an
/// unauthenticated login page is Prompt 14's job — only the entity ships now.
/// </summary>
public sealed class TenantBranding : ITenantScoped
{
    public Guid TenantId { get; set; }

    public string? ProductName { get; set; }

    public string? LogoUrl { get; set; }

    public string? FaviconUrl { get; set; }

    /// <summary>Hex colour, e.g. <c>"#c2410c"</c>.</summary>
    public string? AccentColor { get; set; }

    /// <summary>Hex colour, e.g. <c>"#ffffff"</c>.</summary>
    public string? SurfaceColor { get; set; }

    public string? LoginTagline { get; set; }

    /// <summary><c>"Light"</c>, <c>"Dark"</c>, or <c>"Auto"</c>.</summary>
    public string ThemeMode { get; set; } = "Light";
}
