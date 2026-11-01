namespace WazzufJobs.BLL.Features.Companies.Commands.ChangeMemberRole;

public class ChangeCompanyMemberRoleCommandValidator : AbstractValidator<ChangeCompanyMemberRoleCommand>
{
    public ChangeCompanyMemberRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum().WithMessage("Role must be Owner, Admin or Recruiter.");
    }
}
