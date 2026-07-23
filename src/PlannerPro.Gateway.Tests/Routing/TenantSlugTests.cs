using Microsoft.AspNetCore.Http;
using PlannerPro.Gateway.Routing;

namespace PlannerPro.Gateway.Tests.Routing;

public sealed class TenantSlugTests
{
    [Fact]
    public void TryExtract_ValidSlug_ReturnsSlugAndRemainder()
    {
        var ok = TenantSlug.TryExtract("/api/t/acme/board/current", out var slug, out var remainder);

        Assert.True(ok);
        Assert.Equal("acme", slug);
        Assert.Equal(new PathString("/board/current"), remainder);
    }

    [Fact]
    public void TryExtract_SlugOnlyNoRemainder_ReturnsEmptyRemainder()
    {
        var ok = TenantSlug.TryExtract("/api/t/acme", out var slug, out var remainder);

        Assert.True(ok);
        Assert.Equal("acme", slug);
        Assert.Equal(new PathString("/"), remainder);
    }

    [Fact]
    public void TryExtract_NotUnderTenantPrefix_ReturnsFalse()
    {
        Assert.False(TenantSlug.TryExtract("/api/public/whatever", out _, out _));
    }

    [Fact]
    public void TryExtract_NoSlugSegment_ReturnsFalse()
    {
        Assert.False(TenantSlug.TryExtract("/api/t", out _, out _));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("auth")]
    [InlineData("admin")]
    [InlineData("app")]
    [InlineData("www")]
    [InlineData("t")]
    [InlineData("signup")]
    [InlineData("login")]
    [InlineData("health")]
    [InlineData("public")]
    [InlineData("assets")]
    [InlineData("static")]
    public void TryExtract_ReservedSlug_ReturnsFalse(string reserved)
    {
        Assert.False(TenantSlug.TryExtract($"/api/t/{reserved}/board", out _, out _));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ab")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("ac me")]
    [InlineData("UPPER")]
    public void TryExtract_MalformedSlug_ReturnsFalse(string malformed)
    {
        Assert.False(TenantSlug.TryExtract($"/api/t/{malformed}/board", out _, out _));
    }

    [Fact]
    public void TryExtract_MinimalValidLengthSlug_ReturnsTrue()
    {
        Assert.True(TenantSlug.TryExtract("/api/t/ab1/board", out var slug, out _));
        Assert.Equal("ab1", slug);
    }
}
