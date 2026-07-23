using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;

namespace PlannerPro.Access.Tests.Business;

public sealed class AuthBusinessTests : IDisposable
{
    private readonly IdentityTestHarness _harness = new();

    [Fact]
    public async Task AuthenticateAsync_WithCorrectPassword_ReturnsSession()
    {
        var user = new ApplicationUser { UserName = "owner@acme.test", Email = "owner@acme.test", IsPlatformAdmin = true };
        await _harness.UserManager.CreateAsync(user, "Correct-Horse-1");

        var business = new AuthBusiness(_harness.UserManager, _harness.SignInManager);
        var session = await business.AuthenticateAsync("owner@acme.test", "Correct-Horse-1");

        Assert.NotNull(session);
        Assert.Equal(user.Id, session!.UserId);
        Assert.Equal("owner@acme.test", session.Email);
        Assert.True(session.IsPlatformAdmin);
    }

    [Fact]
    public async Task AuthenticateAsync_WithWrongPassword_ReturnsNull()
    {
        var user = new ApplicationUser { UserName = "member@acme.test", Email = "member@acme.test" };
        await _harness.UserManager.CreateAsync(user, "Correct-Horse-1");

        var business = new AuthBusiness(_harness.UserManager, _harness.SignInManager);
        var session = await business.AuthenticateAsync("member@acme.test", "wrong-password");

        Assert.Null(session);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnknownEmail_ReturnsNullTheSameWayAsWrongPassword()
    {
        var business = new AuthBusiness(_harness.UserManager, _harness.SignInManager);
        var session = await business.AuthenticateAsync("nobody@acme.test", "whatever-1");

        Assert.Null(session);
    }

    public void Dispose() => _harness.Dispose();
}
