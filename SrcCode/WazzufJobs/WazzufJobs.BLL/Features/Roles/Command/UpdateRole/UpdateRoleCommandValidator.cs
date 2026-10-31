namespace WazzufJobs.BLL.Features.Roles.Command.UpdateRole;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty();

        RuleFor(x => x.roleRequest.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(256).WithMessage("Role name must not exceed 256 characters.");

        // an empty list was always accepted by the API (a role without permissions)
        RuleFor(x => x.roleRequest.Permissions)
            .NotNull().WithMessage("Permissions are required.");

        RuleFor(x => x.roleRequest.Permissions)
            .Must(x => x.Distinct().Count() == x.Count)
            .WithMessage("Duplicate permissions are not allowed.")
            .When(x => x.roleRequest.Permissions != null);
    }
}