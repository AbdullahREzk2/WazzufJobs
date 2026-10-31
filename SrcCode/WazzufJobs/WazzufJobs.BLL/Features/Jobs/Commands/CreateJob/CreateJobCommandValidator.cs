namespace WazzufJobs.BLL.Features.Jobs.Commands.CreateJob;

public class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Request.Description)
            .NotEmpty().WithMessage("Description is required.");

        // location and skills are optional in the admin UI
        RuleFor(x => x.Request.Location)
            .MaximumLength(200).WithMessage("Location cannot exceed 200 characters.");

        RuleFor(x => x.Request.CategoryId)
            .GreaterThan(0).WithMessage("Valid category is required.");

        RuleFor(x => x.Request.SalaryMin)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum salary cannot be negative.")
            .When(x => x.Request.SalaryMin.HasValue);

        RuleFor(x => x.Request.SalaryMax)
            .GreaterThanOrEqualTo(x => x.Request.SalaryMin)
            .WithMessage("Maximum salary cannot be less than minimum salary.")
            .When(x => x.Request.SalaryMax.HasValue && x.Request.SalaryMin.HasValue);
    }
}