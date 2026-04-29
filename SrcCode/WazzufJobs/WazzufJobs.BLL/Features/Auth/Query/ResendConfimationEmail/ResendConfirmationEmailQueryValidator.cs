namespace WazzufJobs.BLL.Features.Auth.Query.ResendConfimationEmail;

public class ResendConfirmationEmailQueryValidator : AbstractValidator<ResendConfirmationEmailQuery>
{
    public ResendConfirmationEmailQueryValidator()
    {
        RuleFor(x => x.emailRequest.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email format.");
    }
}