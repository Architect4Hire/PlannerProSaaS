using FluentValidation;
using PlannerPro.Access.Core.Managers.Models.ViewModels;

namespace PlannerPro.Access.Core.Managers.Validators;

/// <summary>Slug rules and the reserved-word list are ADR-0007's, verbatim — a slug shares a
/// namespace with routes, so a reserved word here would create an unroutable tenant.</summary>
public sealed class SignupViewModelValidator : AbstractValidator<SignupViewModel>
{
    private static readonly string[] ReservedSlugs =
    [
        "api", "auth", "admin", "app", "www", "t", "signup", "login", "health", "public", "assets", "static",
    ];

    public SignupViewModelValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .Matches("^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$")
            .Must(slug => !ReservedSlugs.Contains(slug))
            .WithMessage("'{PropertyName}' is a reserved word and cannot be used as a tenant slug.");

        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.OwnerPassword).NotEmpty();
    }
}
