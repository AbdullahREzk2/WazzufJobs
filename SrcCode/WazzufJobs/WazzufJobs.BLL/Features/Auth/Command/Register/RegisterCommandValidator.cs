namespace WazzufJobs.BLL.Features.Auth.Command.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.requestDTO.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email format.");

        RuleFor(x => x.requestDTO.Password)
            .NotEmpty().WithMessage("Password cannot be empty.")
            .Matches(RegexPatterns.Password)
            .WithMessage("Password must be at least 8 characters, containing at least 1 uppercase, 1 lowercase, 1 digit, and 1 special character.");

        RuleFor(x => x.requestDTO.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.requestDTO.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");
    }
}