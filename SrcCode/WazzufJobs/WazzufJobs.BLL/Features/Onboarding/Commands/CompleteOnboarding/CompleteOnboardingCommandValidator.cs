namespace WazzufJobs.BLL.Features.Onboarding.Commands.CompleteOnboarding;

public class CompleteOnboardingCommandValidator: AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.Request.ExperienceYears)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Experience years cannot be negative.")
            .LessThanOrEqualTo(50)
            .WithMessage("Experience years cannot exceed 50.");

        RuleFor(x => x.Request.PreferredJobTypes)
            .NotEmpty()
            .WithMessage("Select at least one preferred job type.");

        RuleFor(x => x.Request.PreferredWorkplaceTypes)
            .NotEmpty()
            .WithMessage("Select at least one preferred workplace type.");

        // interested categories and job titles are optional in the onboarding UI

        RuleFor(x => x.Request.MinSalary)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Minimum salary cannot be negative.")
            .When(x => x.Request.MinSalary.HasValue);
    }
}