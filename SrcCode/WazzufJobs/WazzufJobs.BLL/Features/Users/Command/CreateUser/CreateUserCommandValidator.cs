namespace WazzufJobs.BLL.Features.Users.Command.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.userRequest.firstName)
            .NotEmpty().WithMessage("FirstName cannot be empty.")
            .Length(3, 100).WithMessage("FirstName length must be between 3 and 100 characters.");

        RuleFor(x => x.userRequest.lastName)
            .NotEmpty().WithMessage("LastName cannot be empty.")
            .Length(3, 100).WithMessage("LastName length must be between 3 and 100 characters.");

        RuleFor(x => x.userRequest.Email)
            .NotEmpty().WithMessage("Email cannot be empty.")
            .EmailAddress().WithMessage("Email must be a valid email format.");

        RuleFor(x => x.userRequest.Password)
            .NotEmpty().WithMessage("Password cannot be empty.")
            .Matches(RegexPatterns.Password)
            .WithMessage("Password must be at least 8 characters, containing at least 1 uppercase, 1 lowercase, 1 digit, and 1 special character.");

        RuleFor(x => x.userRequest.Roles)
            .NotNull()
            .NotEmpty().WithMessage("At least one role is required.");

        RuleFor(x => x.userRequest.Roles)
            .Must(x => x.Distinct().Count() == x.Count)
            .WithMessage("Duplicate roles are not allowed.")
            .When(x => x.userRequest.Roles != null);
    }
}