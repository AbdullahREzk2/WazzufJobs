namespace WazzufJobs.BLL.Features.Auth.Query.SendResetPassword;

public class SendResetPasswordQueryValidator : AbstractValidator<SendResetPasswordQuery>
{
    public SendResetPasswordQueryValidator()
    {
        RuleFor(x => x.passRequest.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");
    }
}