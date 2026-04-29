namespace WazzufJobs.BLL.Features.Roles.Command.UpdateRole;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty();

        RuleFor(x => x.roleRequest.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .Length(3, 200).WithMessage("Role name must be between 3 and 200 characters.");

        RuleFor(x => x.roleRequest.Permissions)
            .NotNull()
            .NotEmpty().WithMessage("At least one permission is required.");

        RuleFor(x => x.roleRequest.Permissions)
            .Must(x => x.Distinct().Count() == x.Count)
            .WithMessage("Duplicate permissions are not allowed.")
            .When(x => x.roleRequest.Permissions != null);
    }
}