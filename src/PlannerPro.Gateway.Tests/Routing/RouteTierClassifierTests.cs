using PlannerPro.Gateway.Routing;

namespace PlannerPro.Gateway.Tests.Routing;

public sealed class RouteTierClassifierTests
{
    [Theory]
    [InlineData("/api/ping")]
    [InlineData("/api/public/tenants/acme/branding")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/signup")]
    [InlineData("/api/invitations/abc123")]
    public void Classify_AnonymousPaths_ReturnsAnonymous(string path)
    {
        Assert.Equal(RouteTier.Anonymous, RouteTierClassifier.Classify(path));
    }

    [Theory]
    [InlineData("/api/me/tenants")]
    [InlineData("/api/admin/tenants")]
    public void Classify_AuthenticatedNonTenantPaths_ReturnsAuthenticatedNonTenant(string path)
    {
        Assert.Equal(RouteTier.AuthenticatedNonTenant, RouteTierClassifier.Classify(path));
    }

    [Theory]
    [InlineData("/api/t/acme/board")]
    [InlineData("/api/t/acme")]
    public void Classify_TenantScopedPaths_ReturnsTenantScoped(string path)
    {
        Assert.Equal(RouteTier.TenantScoped, RouteTierClassifier.Classify(path));
    }

    [Fact]
    public void Classify_UnlistedPath_FailsClosedToAuthenticatedNonTenant()
    {
        Assert.Equal(RouteTier.AuthenticatedNonTenant, RouteTierClassifier.Classify("/api/something-new"));
    }
}
