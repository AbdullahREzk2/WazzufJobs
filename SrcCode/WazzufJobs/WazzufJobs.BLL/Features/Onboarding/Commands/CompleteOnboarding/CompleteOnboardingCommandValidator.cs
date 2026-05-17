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

        RuleFor(x => x.Request.InterestedCategoryIds)
            .NotEmpty()
            .WithMessage("Select at least one job category.");

        RuleFor(x => x.Request.InterestedJobTitles)
            .NotEmpty()
            .WithMessage("Enter at least one interested job title.");

        RuleFor(x => x.Request.MinSalary)
            .GreaterThan(0)
            .WithMessage("Minimum salary must be greater than 0.")
            .When(x => x.Request.MinSalary.HasValue);
    }
}