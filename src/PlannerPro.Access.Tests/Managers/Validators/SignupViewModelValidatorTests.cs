using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Access.Core.Managers.Validators;

namespace PlannerPro.Access.Tests.Managers.Validators;

public sealed class SignupViewModelValidatorTests
{
    private readonly SignupViewModelValidator _validator = new();

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
    public void Slug_ReservedWord_Fails(string slug)
    {
        var result = _validator.Validate(Build(slug: slug));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, f => f.PropertyName == nameof(SignupViewModel.Slug));
    }

    [Theory]
    [InlineData("ab")] // 2 chars — below the 3-char minimum
    [InlineData("Acme")] // uppercase
    [InlineData("acme_co")] // underscore not allowed
    [InlineData("acme co")] // space not allowed
    [InlineData("-acme")] // must start with alphanumeric
    [InlineData("acme-")] // must end with alphanumeric
    public void Slug_InvalidShapeOrLength_Fails(string slug)
    {
        var result = _validator.Validate(Build(slug: slug));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, f => f.PropertyName == nameof(SignupViewModel.Slug));
    }

    [Fact]
    public void Slug_ThreeCharMinimum_Passes()
    {
        var result = _validator.Validate(Build(slug: "abc"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Slug_ThirtyTwoCharMaximum_Passes()
    {
        var slug = "a" + new string('b', 30) + "c"; // 32 chars total
        var result = _validator.Validate(Build(slug: slug));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Slug_ThirtyThreeChars_Fails()
    {
        var slug = "a" + new string('b', 31) + "c"; // 33 chars total
        var result = _validator.Validate(Build(slug: slug));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, f => f.PropertyName == nameof(SignupViewModel.Slug));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner@")]
    [InlineData("@acme.test")]
    [InlineData("owner acme.test")]
    public void OwnerEmail_Malformed_Fails(string email)
    {
        var result = _validator.Validate(Build(ownerEmail: email));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, f => f.PropertyName == nameof(SignupViewModel.OwnerEmail));
    }

    [Fact]
    public void TenantName_AtTwoHundredChars_Passes()
    {
        var result = _validator.Validate(Build(tenantName: new string('a', 200)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void TenantName_OverTwoHundredChars_Fails()
    {
        var result = _validator.Validate(Build(tenantName: new string('a', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, f => f.PropertyName == nameof(SignupViewModel.TenantName));
    }

    [Fact]
    public void OwnerPassword_Empty_Fails()
    {
        var result = _validator.Validate(Build(ownerPassword: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, f => f.PropertyName == nameof(SignupViewModel.OwnerPassword));
    }

    [Fact]
    public void ValidViewModel_Passes()
    {
        var result = _validator.Validate(Build());

        Assert.True(result.IsValid);
    }

    private static SignupViewModel Build(
        string slug = "acme", string tenantName = "Acme Inc", string ownerEmail = "owner@acme.test", string ownerPassword = "Correct-Horse-1") =>
        new(slug, tenantName, ownerEmail, ownerPassword);
}
