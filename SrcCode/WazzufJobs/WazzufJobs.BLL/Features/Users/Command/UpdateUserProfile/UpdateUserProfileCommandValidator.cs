namespace SurveyBasket.BLL.Features.Users.Command.UpdateUserProfile;

public class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.userId)
            .NotEmpty();

        RuleFor(x => x.Profilerequest.firstName)
            .NotEmpty().WithMessage("First name is required.")
            .Length(3, 100).WithMessage("First name must be between 3 and 100 characters.");

        RuleFor(x => x.Profilerequest.lastName)
            .NotEmpty().WithMessage("Last name is required.")
            .Length(3, 100).WithMessage("Last name must be between 3 and 100 characters.");
    }
}