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

        RuleFor(x => x.Request.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(200).WithMessage("Location cannot exceed 200 characters.");

        RuleFor(x => x.Request.Skills)
            .NotEmpty().WithMessage("At least one skill is required.");

        RuleFor(x => x.Request.CategoryId)
            .GreaterThan(0).WithMessage("Valid category is required.");

        RuleFor(x => x.Request.SalaryMin)
            .GreaterThan(0).WithMessage("Minimum salary must be greater than 0.")
            .When(x => x.Request.SalaryMin.HasValue);

        RuleFor(x => x.Request.SalaryMax)
            .GreaterThan(x => x.Request.SalaryMin)
            .WithMessage("Maximum salary must be greater than minimum salary.")
            .When(x => x.Request.SalaryMax.HasValue && x.Request.SalaryMin.HasValue);
    }
}