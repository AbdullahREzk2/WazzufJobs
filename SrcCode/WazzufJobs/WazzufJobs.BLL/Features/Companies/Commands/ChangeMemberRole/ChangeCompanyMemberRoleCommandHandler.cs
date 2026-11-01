using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Features.Companies.Commands.ChangeMemberRole;

public class ChangeCompanyMemberRoleCommandHandler(ICompanyAccessService access, ICompanyRepository companies)
    : IRequestHandler<ChangeCompanyMemberRoleCommand, Result>
{
    public async Task<Result> Handle(ChangeCompanyMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var allowed = await access.RequireAsync(request.CompanyId, CompanyPermission.ManageTeam, cancellationToken);
        if (!allowed.IsSuccess)
            return allowed;

        return await companies.RunExclusiveForCompanyAsync(request.CompanyId, async () =>
        {
            // re-checked under the company lock: a concurrent change may have removed or demoted the caller
            var actor = await access.RequireAsync(request.CompanyId, CompanyPermission.ManageTeam, cancellationToken);
            if (!actor.IsSuccess)
                return actor;

            var target = await companies.GetActiveMemberAsync(request.CompanyId, request.UserId, cancellationToken);
            if (target is null)
                return Result.Failure(CompanyErrors.MemberNotFound);

            if (actor.Value.Role != CompanyMemberRole.Owner && (target.Role == CompanyMemberRole.Owner || request.Role == CompanyMemberRole.Owner))
                return Result.Failure(CompanyErrors.OwnersManagedByOwners);

            if (target.Role == request.Role)
                return Result.Success();

            if (target.Role == CompanyMemberRole.Owner && !target.UserDisabled
                && await companies.CountActiveOwnersAsync(request.CompanyId, cancellationToken) <= 1)
                return Result.Failure(CompanyErrors.LastOwner);

            return await companies.SetMemberRoleAsync(request.CompanyId, request.UserId, request.Role, cancellationToken)
                ? Result.Success()
                : Result.Failure(CompanyErrors.MemberNotFound);
        }, result => result.IsSuccess, cancellationToken);
    }
}
