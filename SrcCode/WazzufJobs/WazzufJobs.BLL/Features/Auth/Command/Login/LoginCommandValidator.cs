namespace WazzufJobs.BLL.Features.Auth.Command.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email format.");

        RuleFor(x => x.Password)
            // no length rule: a wrong password must stay an authentication failure (401), not a validation error
            .NotEmpty().WithMessage("Password is required.");
    }
}