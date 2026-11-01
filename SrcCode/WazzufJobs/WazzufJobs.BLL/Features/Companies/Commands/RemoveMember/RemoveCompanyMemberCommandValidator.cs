namespace WazzufJobs.BLL.Features.Companies.Commands.RemoveMember;

public class RemoveCompanyMemberCommandValidator : AbstractValidator<RemoveCompanyMemberCommand>
{
    public RemoveCompanyMemberCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
