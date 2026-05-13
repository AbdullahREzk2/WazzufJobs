namespace WazzufJobs.BLL.Features.Auth.Command.ConfirmEmail;

public class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(x => x.emailRequest.UserId)
            .NotEmpty().WithMessage("UserId cannot be empty.");

        RuleFor(x => x.emailRequest.Code)
            .NotEmpty().WithMessage("Code cannot be empty.");
    }
}