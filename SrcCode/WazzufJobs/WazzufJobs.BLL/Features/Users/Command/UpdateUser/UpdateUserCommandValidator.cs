namespace WazzufJobs.BLL.Features.Users.Command.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.userId)
            .NotEmpty();

        RuleFor(x => x.userRequest.firstName)
            .NotEmpty().WithMessage("FirstName cannot be empty.")
            .MaximumLength(100).WithMessage("FirstName must not exceed 100 characters.");

        RuleFor(x => x.userRequest.lastName)
            .NotEmpty().WithMessage("LastName cannot be empty.")
            .MaximumLength(100).WithMessage("LastName must not exceed 100 characters.");

        RuleFor(x => x.userRequest.Email)
            .NotEmpty().WithMessage("Email cannot be empty.")
            .EmailAddress().WithMessage("Email must be a valid email format.");

        // an empty list is valid: it removes every role (covered by AdminAccountSafetyTests)
        RuleFor(x => x.userRequest.Roles)
            .NotNull().WithMessage("Roles are required.");

        RuleFor(x => x.userRequest.Roles)
            .Must(x => x.Distinct().Count() == x.Count)
            .WithMessage("Duplicate roles are not allowed.")
            .When(x => x.userRequest.Roles != null);
    }
}