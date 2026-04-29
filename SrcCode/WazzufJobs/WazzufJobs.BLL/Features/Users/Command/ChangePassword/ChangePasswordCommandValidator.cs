namespace WazzufJobs.BLL.Features.Users.Command.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.userId)
            .NotEmpty();

        RuleFor(x => x.Passrequest.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required.");

        RuleFor(x => x.Passrequest.NewPassword)
            .NotEmpty()
            .WithMessage("Password Cannot be Empty!")
            .Matches(RegexPatterns.Password)
            .WithMessage("Password should be at least 8 characters, containing at least 1 uppercase, 1 lowercase, 1 digit, and 1 special character.")
            .NotEqual(x => x.Passrequest.CurrentPassword)
            .WithMessage("New password cannot be the same as the current password.");
    }
}