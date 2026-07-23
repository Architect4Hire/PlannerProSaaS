using FluentValidation;
using PlannerPro.Access.Core.Managers.Models.ViewModels;

namespace PlannerPro.Access.Core.Managers.Validators;

public sealed class LoginViewModelValidator : AbstractValidator<LoginViewModel>
{
    public LoginViewModelValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
