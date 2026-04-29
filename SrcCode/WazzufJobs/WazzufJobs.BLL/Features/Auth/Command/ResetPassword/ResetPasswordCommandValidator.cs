namespace WazzufJobs.BLL.Features.Auth.Command.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.passRequest.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email format.");

        RuleFor(x => x.passRequest.Code)
            .NotEmpty().WithMessage("Code is required.");

        RuleFor(x => x.passRequest.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .Matches(RegexPatterns.Password)
            .WithMessage("Password must be at least 8 characters, containing at least 1 uppercase, 1 lowercase, 1 digit, and 1 special character.");
    }
}